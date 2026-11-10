using System.Management;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación de <see cref="IDriveMediaService"/> usando WMI:
/// - MSFT_PhysicalDisk (root\Microsoft\Windows\Storage) para MediaType preciso (SSD / HDD / NVMe)
/// - Win32_DiskDrive (root\cimv2) como fallback si el espacio de nombres Storage no está disponible
/// </summary>
public sealed class DriveMediaService : IDriveMediaService, IDisposable
{
    // Cache para no repetir la consulta WMI en cada scan
    private List<PhysicalDriveInfo>? _cachedDrives;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public void Dispose() => _lock.Dispose();

    public async Task<IReadOnlyList<PhysicalDriveInfo>> GetAllDrivesAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedDrives is not null)
            {
                return _cachedDrives.AsReadOnly();
            }

            _cachedDrives = await Task.Run(QueryDrives, cancellationToken);
            return _cachedDrives.AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DriveMediaType> GetSystemDriveMediaTypeAsync(CancellationToken cancellationToken = default)
    {
        var drives = await GetAllDrivesAsync(cancellationToken);
        var systemDrive = drives.FirstOrDefault(d => d.IsSystemDrive);
        return systemDrive?.MediaType ?? DriveMediaType.Unknown;
    }

    public async Task<bool> IsSystemDriveSolidStateAsync(CancellationToken cancellationToken = default)
    {
        var mediaType = await GetSystemDriveMediaTypeAsync(cancellationToken);
        return mediaType is DriveMediaType.Ssd or DriveMediaType.Nvme;
    }

    // -------------------------------------------------------------------------
    // Lógica de consulta WMI (síncrona, ejecutada en Task.Run)
    // -------------------------------------------------------------------------

    private static List<PhysicalDriveInfo> QueryDrives()
    {
        // Intentamos primero MSFT_PhysicalDisk que da MediaType más preciso
        var drives = TryQueryMsftPhysicalDisk();
        if (drives.Count > 0)
        {
            return drives;
        }

        // Fallback: Win32_DiskDrive
        return QueryWin32DiskDrive();
    }

    /// <summary>
    /// Consulta MSFT_PhysicalDisk en root\Microsoft\Windows\Storage.
    /// MediaType: 0=Unspecified, 3=HDD, 4=SSD, 5=SCM/NVMe
    /// BusType: 17=NVMe
    /// </summary>
    private static List<PhysicalDriveInfo> TryQueryMsftPhysicalDisk()
    {
        var results = new List<PhysicalDriveInfo>();

        try
        {
            string systemDriveLetter = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                                       ?? "C:\\";

            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk");

            using var collection = searcher.Get();

            foreach (ManagementObject disk in collection)
            {
                var mediaTypeRaw = Convert.ToInt32(disk["MediaType"] ?? 0, System.Globalization.CultureInfo.InvariantCulture);
                var busTypeRaw   = Convert.ToInt32(disk["BusType"] ?? 0, System.Globalization.CultureInfo.InvariantCulture);
                var size         = Convert.ToInt64(disk["Size"] ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
                var name         = disk["FriendlyName"]?.ToString() ?? "Disco Desconocido";

                // BusType 17 = NVMe → aunque MediaType diga 4 (SSD), lo marcamos como NVMe
                var mediaType = busTypeRaw == 17
                    ? DriveMediaType.Nvme
                    : mediaTypeRaw switch
                    {
                        3 => DriveMediaType.Hdd,
                        4 => DriveMediaType.Ssd,
                        5 => DriveMediaType.Nvme,
                        _ => DriveMediaType.Unknown
                    };

                results.Add(new PhysicalDriveInfo
                {
                    FriendlyName  = name,
                    MediaType     = mediaType,
                    SizeBytes     = size,
                    // Aproximación: marcamos el primero como sistema
                    // La correlación exacta requeriría cruzar con Win32_DiskPartition
                    IsSystemDrive = results.Count == 0
                });

                disk.Dispose();
            }
        }
        catch
        {
            // El espacio de nombres Storage puede no estar disponible en algunas imágenes de Windows
        }

        return results;
    }

    /// <summary>
    /// Fallback: consulta Win32_DiskDrive en root\cimv2.
    /// Detecta SSD usando MediaType == "SSD" o el campo InterfaceType == "SCSI" + nombre.
    /// </summary>
    private static List<PhysicalDriveInfo> QueryWin32DiskDrive()
    {
        var results = new List<PhysicalDriveInfo>();

        try
        {
            // Obtenemos la letra del disco del sistema
            string systemDriveRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";

            // Win32_DiskDrive → correlacionar con partición del sistema
            var systemDiskIndexes = GetSystemDiskIndexes(systemDriveRoot);

            using var searcher = new ManagementObjectSearcher(
                "SELECT Index, Caption, MediaType, InterfaceType, Size FROM Win32_DiskDrive");
            using var collection = searcher.Get();

            foreach (ManagementObject disk in collection)
            {
                var index         = Convert.ToInt32(disk["Index"] ?? 0, System.Globalization.CultureInfo.InvariantCulture);
                var caption       = disk["Caption"]?.ToString() ?? "Disco Desconocido";
                var mediaTypeStr  = disk["MediaType"]?.ToString() ?? string.Empty;
                var interfaceType = disk["InterfaceType"]?.ToString() ?? string.Empty;
                var size          = Convert.ToInt64(disk["Size"] ?? 0L, System.Globalization.CultureInfo.InvariantCulture);

                // Heurística para detectar SSD desde Win32_DiskDrive
                DriveMediaType mediaType;
                if (interfaceType.Equals("SCSI", StringComparison.OrdinalIgnoreCase) &&
                    (caption.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                     caption.Contains("M.2",  StringComparison.OrdinalIgnoreCase)))
                {
                    mediaType = DriveMediaType.Nvme;
                }
                else if (mediaTypeStr.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                         caption.Contains("SSD",  StringComparison.OrdinalIgnoreCase))
                {
                    mediaType = DriveMediaType.Ssd;
                }
                else if (mediaTypeStr.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ||
                         mediaTypeStr.Contains("spinning", StringComparison.OrdinalIgnoreCase))
                {
                    mediaType = DriveMediaType.Hdd;
                }
                else
                {
                    mediaType = DriveMediaType.Unknown;
                }

                results.Add(new PhysicalDriveInfo
                {
                    FriendlyName  = caption,
                    MediaType     = mediaType,
                    SizeBytes     = size,
                    IsSystemDrive = systemDiskIndexes.Contains(index)
                });

                disk.Dispose();
            }
        }
        catch
        {
            // Si WMI no responde devolvemos lista vacía
        }

        return results;
    }

    /// <summary>
    /// Obtiene los índices de disco (Win32_DiskDrive.Index) que contienen
    /// la partición donde está instalado Windows.
    /// </summary>
    private static HashSet<int> GetSystemDiskIndexes(string systemDriveRoot)
    {
        var indexes = new HashSet<int>();
        try
        {
            // Win32_LogicalDisk → Win32_LogicalDiskToPartition → Win32_DiskDriveToDiskPartition
            string driveLetter = systemDriveRoot.TrimEnd('\\');

            using var ldSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
            using var partitions = ldSearcher.Get();

            foreach (ManagementObject partition in partitions)
            {
                string partitionId = partition["DeviceID"]?.ToString() ?? string.Empty;

                using var ddSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                using var drives = ddSearcher.Get();

                foreach (ManagementObject drive in drives)
                {
                    indexes.Add(Convert.ToInt32(drive["Index"] ?? 0, System.Globalization.CultureInfo.InvariantCulture));
                    drive.Dispose();
                }

                partition.Dispose();
            }
        }
        catch { /* fallback: asume disco 0 */ indexes.Add(0); }

        return indexes.Count > 0 ? indexes : new HashSet<int> { 0 };
    }
}
