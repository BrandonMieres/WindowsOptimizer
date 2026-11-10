using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace WindowsOptimizer_App.ViewModels;

public sealed class SystemInfoViewModel : ObservableObject
{
    private string _osVersion = "Cargando...";
    public string OsVersion
    {
        get => _osVersion;
        set => SetProperty(ref _osVersion, value);
    }

    private string _processor = "Cargando...";
    public string Processor
    {
        get => _processor;
        set => SetProperty(ref _processor, value);
    }

    private string _ramAmount = "Cargando...";
    public string RamAmount
    {
        get => _ramAmount;
        set => SetProperty(ref _ramAmount, value);
    }

    private string _ramSpeed = "Cargando...";
    public string RamSpeed
    {
        get => _ramSpeed;
        set => SetProperty(ref _ramSpeed, value);
    }

    private string _graphicsCard = "Cargando...";
    public string GraphicsCard
    {
        get => _graphicsCard;
        set => SetProperty(ref _graphicsCard, value);
    }

    private string _disks = "Cargando...";
    public string Disks
    {
        get => _disks;
        set => SetProperty(ref _disks, value);
    }

    private string _xmpStatus = "Comprobando...";
    public string XmpStatus
    {
        get => _xmpStatus;
        set => SetProperty(ref _xmpStatus, value);
    }

    private string _resizableBarStatus = "Comprobando...";
    public string ResizableBarStatus
    {
        get => _resizableBarStatus;
        set => SetProperty(ref _resizableBarStatus, value);
    }

    public SystemInfoViewModel() {}

    public async void LoadSystemInfo(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
    {
        if (OsVersion != "Cargando...") return;

        var (os, cpu, gpu, ramAmount, ramSpeed, disks, xmp, sam) = await Task.Run(() =>
        {
            string outOs = "Desconocido";
            string outCpu = "Desconocido";
            string outGpu = "Desconocida";
            string outRamAmt = "Desconocido";
            string outRamSpd = "Desconocida";
            string outDisks = "Ninguno detectado";
            string outXmp = "Desconocido";
            string outSam = "Comprobando...";

            try
            {
                // OS
                outOs = RuntimeInformation.OSDescription;
            }
            catch { }

            try
            {
                // CPU
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    outCpu = obj["Name"]?.ToString() ?? "Desconocido";
                    break;
                }
            }
            catch { }

            try
            {
                // GPU
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                List<string> gpus = new();
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        gpus.Add(name);
                }
                if (gpus.Count > 0)
                    outGpu = string.Join(" / ", gpus);
            }
            catch { }

            try
            {
                // RAM amount (capacity)
                using var searcherMem = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                ulong totalRam = 0;
                foreach (var obj in searcherMem.Get())
                {
                    if (obj["Capacity"] != null && ulong.TryParse(obj["Capacity"].ToString(), out ulong cap))
                    {
                        totalRam += cap;
                    }
                }
                outRamAmt = totalRam > 0 ? $"{totalRam / (1024 * 1024 * 1024)} GB" : "Desconocido";

                // RAM speed
                using var searcherSpeed = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory");
                string speed = "";
                foreach (var obj in searcherSpeed.Get())
                {
                    if (obj["Speed"] != null)
                    {
                        speed = obj["Speed"]?.ToString() ?? "";
                        break; // usually all sticks run at the same speed
                    }
                }
                outRamSpd = !string.IsNullOrEmpty(speed) ? $"{speed} MHz" : "Desconocida";

                // XMP Status Check based on RAM speed
                if (!string.IsNullOrEmpty(speed) && int.TryParse(speed, out int s))
                {
                    if (s > 2666) outXmp = "Activado";
                    else outXmp = "Desactivado (o base)";
                }
            }
            catch { }

            try
            {
                // Disks
                var diskStrings = new List<string>();
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
                foreach (var d in drives)
                {
                    var sizeGb = d.TotalSize / (1024 * 1024 * 1024);
                    diskStrings.Add($"Disco {d.Name.TrimEnd('\\')} {sizeGb} GB");
                }
                if (diskStrings.Count > 0)
                    outDisks = string.Join("\n", diskStrings);
            }
            catch { }

            try
            {
                // Resizable BAR Check using nvidia-smi
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"$nvsmi = (Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue).Source; if (-not $nvsmi) { $nvsmi = (Get-ChildItem -Path 'C:\\Windows\\System32', 'C:\\Program Files\\NVIDIA Corporation' -Filter 'nvidia-smi.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1) }; if ($nvsmi) { & $nvsmi -q -d MEMORY | Select-String -Pattern 'BAR1' -Context 0,2 }\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (output.Contains("BAR1 Memory Usage"))
                {
                    if (output.Contains("256 MiB"))
                    {
                        outSam = "Desactivado";
                    }
                    else
                    {
                        outSam = "Activado";
                    }
                }
                else
                {
                    outSam = "Desconocido (AMD o no compatible)";
                }
            }
            catch
            {
                outSam = "Error al comprobar";
            }

            return (outOs, outCpu, outGpu, outRamAmt, outRamSpd, outDisks, outXmp, outSam);
        });

        // The continuation after await Task.Run runs on the UI thread
        dispatcher?.TryEnqueue(() => 
        {
            OsVersion = os;
            Processor = cpu;
            GraphicsCard = gpu;
            RamAmount = ramAmount;
            RamSpeed = ramSpeed;
            Disks = disks;
            XmpStatus = xmp;
            ResizableBarStatus = sam;
        });
    }
}



