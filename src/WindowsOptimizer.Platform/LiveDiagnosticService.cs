using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación de ILiveDiagnosticService que diagnostica en vivo cuellos de botella de CPU, RAM y Disco.
/// Identifica culpables conocidos (Windows Update, SearchIndexer, MsMpEng...) sin ser un matador ciego de procesos.
/// </summary>
public sealed class LiveDiagnosticService : ILiveDiagnosticService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

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

    private static readonly HashSet<string> ProtectedSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "services", "lsass",
        "lsm", "svchost", "fontdrvhost", "dwm", "Memory Compression", "winlogon"
    };

    private static readonly Dictionary<string, string> KnownCulprits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SearchIndexer"] = "Indexador de Windows Search. Indexa archivos en segundo plano; si el disco está al 100%, puede deberse a esto.",
        ["TiWorker"] = "Instalador de Windows Update preparando o descargando parches del sistema.",
        ["TrustedInstaller"] = "Servicio de instalación de módulos y actualizaciones de Windows.",
        ["MsMpEng"] = "Antivirus Microsoft Defender realizando análisis o protección en tiempo real.",
        ["CompatTelRunner"] = "Telemetría de compatibilidad de Microsoft recopilando diagnósticos.",
        ["Sedlauncher"] = "Herramienta de mantenimiento de Windows Update.",
        ["chrome"] = "Google Chrome. Con muchas pestañas o extensiones acumula un alto consumo de RAM.",
        ["msedge"] = "Microsoft Edge. Con pestañas abiertas o inicio rápido activo en segundo plano.",
        ["dwm"] = "Desktop Window Manager. Administra la composición gráfica de ventanas.",
        ["audiodg"] = "Aislamiento de audio de Windows. Consumo alto suele indicar controladores de sonido o efectos defectuosos.",
        ["vmmem"] = "Subsistema de virtualización (Docker o WSL) consumiendo recursos asignados.",
        ["explorer"] = "Explorador de Windows (escritorio y barra de tareas)."
    };

    public async Task<LiveResourceSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            // Muestra 1: tomar tiempos iniciales de CPU e I/O
            var sample1 = TakeProcessSample();
            var time1 = Stopwatch.GetTimestamp();

            // Intervalo de muestreo breve (300 ms)
            await Task.Delay(300, cancellationToken);

            // Muestra 2: tomar tiempos finales
            var sample2 = TakeProcessSample();
            var time2 = Stopwatch.GetTimestamp();

            double elapsedSeconds = (double)(time2 - time1) / Stopwatch.Frequency;
            if (elapsedSeconds <= 0) elapsedSeconds = 0.3;

            int cpuCores = Environment.ProcessorCount;
            var processed = new List<ProcessResourceInfo>();
            double totalCpu = 0.0;

            foreach (var kvp in sample2)
            {
                int pid = kvp.Key;
                var p2 = kvp.Value;

                double cpuPct = 0.0;
                long ioRate = 0;

                if (sample1.TryGetValue(pid, out var p1))
                {
                    double cpuMs = (p2.TotalProcessorTime - p1.TotalProcessorTime).TotalMilliseconds;
                    cpuPct = (cpuMs / (elapsedSeconds * 1000.0 * cpuCores)) * 100.0;
                    if (cpuPct < 0) cpuPct = 0;
                    if (cpuPct > 100) cpuPct = 100;

                    ulong deltaIo = (p2.IoReadBytes + p2.IoWriteBytes) > (p1.IoReadBytes + p1.IoWriteBytes)
                        ? (p2.IoReadBytes + p2.IoWriteBytes) - (p1.IoReadBytes + p1.IoWriteBytes)
                        : 0;
                    ioRate = (long)(deltaIo / elapsedSeconds);
                }

                totalCpu += cpuPct;

                bool isSystem = ProtectedSystemProcesses.Contains(p2.Name);
                bool isKnown = KnownCulprits.TryGetValue(p2.Name, out string? note);

                processed.Add(new ProcessResourceInfo
                {
                    Pid = pid,
                    ProcessName = p2.Name,
                    WindowTitle = string.IsNullOrWhiteSpace(p2.MainWindowTitle) ? null : p2.MainWindowTitle,
                    CpuPercentage = Math.Round(cpuPct, 1),
                    MemoryBytes = p2.WorkingSet,
                    MemoryDisplay = FormatBytes(p2.WorkingSet),
                    DiskIoBytesPerSec = ioRate,
                    DiskIoDisplay = FormatRate(ioRate),
                    IsSystemProcess = isSystem,
                    IsKnownCulprit = isKnown,
                    DiagnosisNote = note ?? (isSystem ? "Proceso esencial del sistema operativo Windows." : "Proceso de aplicación de usuario.")
                });
            }

            // Memoria RAM total y usada
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
            catch { }

            ulong usedRam = totalRam > availRam ? totalRam - availRam : 0;
            double ramPct = totalRam > 0 ? (double)usedRam / totalRam * 100.0 : 0.0;
            totalCpu = Math.Min(100.0, Math.Round(totalCpu, 1));

            // Filtrar top de cada recurso
            var topCpu = processed.OrderByDescending(p => p.CpuPercentage).Take(15).ToList();
            var topMem = processed.OrderByDescending(p => p.MemoryBytes).Take(15).ToList();
            var topDisk = processed.OrderByDescending(p => p.DiskIoBytesPerSec).Take(15).ToList();

            // Identificar cuellos de botella
            var bottlenecks = new List<string>();

            if (totalCpu > 80.0)
            {
                var mainCpu = topCpu.FirstOrDefault();
                if (mainCpu != null && mainCpu.CpuPercentage > 25.0)
                {
                    bottlenecks.Add($"Uso muy alto de CPU ({totalCpu}%): {mainCpu.ProcessName} está consumiendo el {mainCpu.CpuPercentage}% de la CPU.");
                }
                else
                {
                    bottlenecks.Add($"Uso global elevado de CPU ({totalCpu}%) distribuido entre varios procesos.");
                }
            }

            if (ramPct > 85.0)
            {
                var mainMem = topMem.FirstOrDefault();
                bottlenecks.Add($"Memoria RAM al límite ({ramPct:F0}%): {mainMem?.ProcessName} está usando {mainMem?.MemoryDisplay}. El sistema podría empezar a usar paginación lenta en disco.");
            }

            var highDisk = topDisk.FirstOrDefault(p => p.DiskIoBytesPerSec > 25L * 1024 * 1024);
            if (highDisk != null)
            {
                bottlenecks.Add($"Actividad intensa en disco: {highDisk.ProcessName} está transfiriendo {highDisk.DiskIoDisplay}.");
            }

            // Detección de culpables específicos activos con alto consumo
            var searchIndexer = processed.FirstOrDefault(p => p.ProcessName.Equals("SearchIndexer", StringComparison.OrdinalIgnoreCase));
            if (searchIndexer != null && (searchIndexer.CpuPercentage > 15.0 || searchIndexer.DiskIoBytesPerSec > 10L * 1024 * 1024))
            {
                bottlenecks.Add("Windows Search está indexando archivos intensamente en este momento.");
            }

            var defender = processed.FirstOrDefault(p => p.ProcessName.Equals("MsMpEng", StringComparison.OrdinalIgnoreCase));
            if (defender != null && defender.CpuPercentage > 20.0)
            {
                bottlenecks.Add("Microsoft Defender Antivirus está realizando un escaneo en segundo plano.");
            }

            var tiWorker = processed.FirstOrDefault(p => p.ProcessName.Equals("TiWorker", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("TrustedInstaller", StringComparison.OrdinalIgnoreCase));
            if (tiWorker != null && tiWorker.CpuPercentage > 15.0)
            {
                bottlenecks.Add("Windows Update está instalando o verificando componentes en segundo plano.");
            }

            // Agrupar procesos por aplicación (como el Administrador de Tareas)
            List<AppProcessGroup> BuildGroups(int sortBy) // 0 = CPU, 1 = RAM, 2 = Disco
            {
                var grouped = processed
                    .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var items = sortBy switch
                        {
                            0 => g.OrderByDescending(p => p.CpuPercentage).ToList(),
                            1 => g.OrderByDescending(p => p.MemoryBytes).ToList(),
                            _ => g.OrderByDescending(p => p.DiskIoBytesPerSec).ToList()
                        };

                        double groupCpu = Math.Round(items.Sum(p => p.CpuPercentage), 1);
                        long groupMem = items.Sum(p => p.MemoryBytes);
                        long groupIo = items.Sum(p => p.DiskIoBytesPerSec);
                        bool isSystem = items.Any(p => p.IsSystemProcess);
                        bool isKnown = items.Any(p => p.IsKnownCulprit);
                        string? mainTitle = items.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.WindowTitle))?.WindowTitle;
                        string note = items.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.DiagnosisNote))?.DiagnosisNote
                                      ?? (isSystem ? "Proceso esencial del sistema operativo Windows." : "Proceso de aplicación de usuario.");

                        return new AppProcessGroup
                        {
                            ProcessName = g.Key,
                            DisplayName = g.Key,
                            MainWindowTitle = mainTitle,
                            TotalCpuPercentage = groupCpu,
                            TotalMemoryBytes = groupMem,
                            TotalMemoryDisplay = FormatBytes(groupMem),
                            TotalDiskIoBytesPerSec = groupIo,
                            TotalDiskIoDisplay = FormatRate(groupIo),
                            IsSystemProcess = isSystem,
                            IsKnownCulprit = isKnown,
                            DiagnosisNote = note,
                            SubProcesses = items
                        };
                    });

                return sortBy switch
                {
                    0 => grouped.OrderByDescending(g => g.TotalCpuPercentage).Take(20).ToList(),
                    1 => grouped.OrderByDescending(g => g.TotalMemoryBytes).Take(20).ToList(),
                    _ => grouped.OrderByDescending(g => g.TotalDiskIoBytesPerSec).Take(20).ToList()
                };
            }

            var topCpuGroups = BuildGroups(0);
            var topMemGroups = BuildGroups(1);
            var topDiskGroups = BuildGroups(2);

            return new LiveResourceSnapshot
            {
                TotalCpuPercentage = totalCpu,
                TotalRamPercentage = Math.Round(ramPct, 1),
                TotalRamUsedBytes = usedRam,
                TotalRamBytes = totalRam,
                TopCpuProcesses = topCpu.AsReadOnly(),
                TopMemoryProcesses = topMem.AsReadOnly(),
                TopDiskProcesses = topDisk.AsReadOnly(),
                TopCpuGroups = topCpuGroups.AsReadOnly(),
                TopMemoryGroups = topMemGroups.AsReadOnly(),
                TopDiskGroups = topDiskGroups.AsReadOnly(),
                Bottlenecks = bottlenecks.AsReadOnly(),
                CapturedAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }

    public Task<bool> TerminateProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (ProtectedSystemProcesses.Contains(process.ProcessName))
                {
                    return false; // Proteger procesos críticos del sistema
                }

                process.Kill(entireProcessTree: true);
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private sealed record ProcessRawSample(
        string Name,
        string MainWindowTitle,
        TimeSpan TotalProcessorTime,
        long WorkingSet,
        ulong IoReadBytes,
        ulong IoWriteBytes);

    private static Dictionary<int, ProcessRawSample> TakeProcessSample()
    {
        var result = new Dictionary<int, ProcessRawSample>();
        var processes = Process.GetProcesses();

        foreach (var p in processes)
        {
            try
            {
                TimeSpan cpu = p.TotalProcessorTime;
                long ws = p.WorkingSet64;
                string name = p.ProcessName;
                string title = string.Empty;
                try { title = p.MainWindowTitle; } catch { }

                ulong readBytes = 0;
                ulong writeBytes = 0;
                try
                {
                    if (GetProcessIoCounters(p.Handle, out var counters))
                    {
                        readBytes = counters.ReadTransferCount;
                        writeBytes = counters.WriteTransferCount;
                    }
                }
                catch { }

                result[p.Id] = new ProcessRawSample(name, title, cpu, ws, readBytes, writeBytes);
            }
            catch
            {
                // Acceso denegado o proceso terminado durante la iteración
            }
            finally
            {
                p.Dispose();
            }
        }

        return result;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double mb = bytes / (1024.0 * 1024.0);
        if (mb >= 1024.0)
        {
            return $"{(mb / 1024.0):F1} GB";
        }
        return $"{mb:F0} MB";
    }

    private static string FormatRate(long bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 KB/s";
        double kb = bytesPerSec / 1024.0;
        if (kb >= 1024.0)
        {
            return $"{(kb / 1024.0):F1} MB/s";
        }
        return $"{kb:F0} KB/s";
    }
}
