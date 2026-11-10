using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Measurement;

/// <summary>
/// Implementación de ISystemBaselineService que recopila métricas reales del sistema Windows.
/// Utiliza APIs nativas de alta eficiencia (GlobalMemoryStatusEx, Event Log ID 100, DriveInfo).
/// </summary>
public sealed partial class SystemBaselineService : ISystemBaselineService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public Task<SystemBaseline> GetBaselineAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            // 1. Memoria RAM física vía Win32 API
            ulong totalRam = 0;
            ulong availRam = 0;
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    totalRam = memStatus.ullTotalPhys;
                    availRam = memStatus.ullAvailPhys;
                }
            }
            catch
            {
                // Fallback a GC/estimación si kernel32 fallara
                totalRam = 0;
                availRam = 0;
            }

            // 2. Tiempo de arranque vía Event Log (Diagnostics-Performance Event 100)
            TimeSpan? bootDuration = GetBootDurationFromEventLog();

            // 3. Procesos activos
            int processCount = 0;
            try
            {
                processCount = Process.GetProcesses().Length;
            }
            catch
            {
                processCount = 0;
            }

            // 4. Tiempo de actividad y última fecha de arranque
            long tickCount = Environment.TickCount64;
            var uptime = TimeSpan.FromMilliseconds(tickCount);
            var lastBoot = DateTimeOffset.UtcNow - uptime;

            // 5. Espacio en disco de la unidad del sistema
            ulong driveFree = 0;
            ulong driveTotal = 0;
            try
            {
                string sysDir = Environment.SystemDirectory;
                string? root = Path.GetPathRoot(sysDir);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                    {
                        driveFree = (ulong)drive.AvailableFreeSpace;
                        driveTotal = (ulong)drive.TotalSize;
                    }
                }
            }
            catch
            {
                driveFree = 0;
                driveTotal = 0;
            }

            return new SystemBaseline
            {
                BootDuration = bootDuration,
                LastBootTime = lastBoot,
                TotalRamBytes = totalRam,
                AvailableRamBytes = availRam,
                ProcessCount = processCount,
                SystemUptime = uptime,
                SystemDriveFreeBytes = driveFree,
                SystemDriveTotalBytes = driveTotal,
                MeasuredAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }

    private static TimeSpan? GetBootDurationFromEventLog()
    {
        try
        {
            const string logName = "Microsoft-Windows-Diagnostics-Performance/Operational";
            const string queryStr = "*[System[(EventID=100)]]";

            var query = new EventLogQuery(logName, PathType.LogName, queryStr)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            using var eventRecord = reader.ReadEvent();
            if (eventRecord != null)
            {
                string xml = eventRecord.ToXml();
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                // EventData contiene <Data Name="BootTime">32500</Data> o <Data Name="MainPathBootTime">...
                var dataElements = doc.Descendants(ns + "Data");
                var bootTimeElement = dataElements.FirstOrDefault(e => (string?)e.Attribute("Name") == "BootTime")
                                   ?? dataElements.FirstOrDefault(e => (string?)e.Attribute("Name") == "MainPathBootTime");

                if (bootTimeElement != null && long.TryParse(bootTimeElement.Value, out long bootTimeMs) && bootTimeMs > 0)
                {
                    return TimeSpan.FromMilliseconds(bootTimeMs);
                }
            }
        }
        catch
        {
            // El log puede no estar disponible o requerir permisos especiales
        }

        return null;
    }
}
