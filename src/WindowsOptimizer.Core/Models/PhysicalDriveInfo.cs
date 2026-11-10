using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Información básica de una unidad física detectada en el sistema.
/// </summary>
public sealed class PhysicalDriveInfo
{
    /// <summary>Letra o ruta de montaje (ej: "C:", "D:").</summary>
    public string MountPoint { get; init; } = string.Empty;

    /// <summary>Nombre amigable del disco (ej: "Samsung SSD 980 PRO 1TB").</summary>
    public string FriendlyName { get; init; } = string.Empty;

    /// <summary>Tipo de medio físico detectado por WMI.</summary>
    public DriveMediaType MediaType { get; init; } = DriveMediaType.Unknown;

    /// <summary>Indica si la unidad es de estado sólido (SSD o NVMe).</summary>
    public bool IsSolid => MediaType is DriveMediaType.Ssd or DriveMediaType.Nvme;

    /// <summary>Es la unidad del sistema operativo.</summary>
    public bool IsSystemDrive { get; init; }

    /// <summary>Tamaño total en bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Descripción legible del tipo de medio.</summary>
    public string MediaTypeLabel => MediaType switch
    {
        DriveMediaType.Nvme => "NVMe",
        DriveMediaType.Ssd  => "SSD",
        DriveMediaType.Hdd  => "HDD",
        _                   => "Desconocido"
    };
}
