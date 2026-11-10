using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class LiveDiagnosticViewModel : ObservableObject
{
    private readonly ILiveDiagnosticService _diagnosticService;
    private readonly IDialogService _dialogService;

    private bool _isCapturing;
    public bool IsCapturing
    {
        get => _isCapturing;
        set
        {
            if (SetProperty(ref _isCapturing, value))
            {
                OnPropertyChanged(nameof(HasNoBottlenecks));
            }
        }
    }

    private string _statusText = "Listo para diagnosticar el sistema";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private LiveResourceSnapshot? _snapshot;
    public LiveResourceSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            if (SetProperty(ref _snapshot, value))
            {
                OnPropertyChanged(nameof(CpuPercentageText));
                OnPropertyChanged(nameof(RamPercentageText));
                OnPropertyChanged(nameof(RamUsageDetails));
                OnPropertyChanged(nameof(HasNoBottlenecks));
            }
        }
    }

    private int _selectedTab; // 0 = CPU, 1 = RAM, 2 = Disco
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                UpdateProcessesList();
            }
        }
    }

    public ObservableCollection<AppProcessGroup> ProcessGroups { get; } = new();
    public ObservableCollection<ProcessResourceInfo> Processes { get; } = new();
    public ObservableCollection<string> Bottlenecks { get; } = new();

    public bool HasBottlenecks => Bottlenecks.Count > 0;
    public bool HasNoBottlenecks => !IsCapturing && Bottlenecks.Count == 0 && Snapshot != null;

    public string CpuPercentageText => $"{Snapshot?.TotalCpuPercentage ?? 0:F0}%";
    public string RamPercentageText => $"{Snapshot?.TotalRamPercentage ?? 0:F0}%";

    public string RamUsageDetails
    {
        get
        {
            if (Snapshot == null || Snapshot.TotalRamBytes == 0) return "Cargando...";
            double usedGb = Snapshot.TotalRamUsedBytes / (1024.0 * 1024.0 * 1024.0);
            double totalGb = Snapshot.TotalRamBytes / (1024.0 * 1024.0 * 1024.0);
            return $"{usedGb:F1} / {totalGb:F0} GB ({Snapshot.TotalRamPercentage:F0}%)";
        }
    }

    public LiveDiagnosticViewModel(ILiveDiagnosticService diagnosticService, IDialogService dialogService)
    {
        _diagnosticService = diagnosticService;
        _dialogService = dialogService;
        _ = CaptureAsync();
    }

    [RelayCommand]
    public async Task CaptureAsync()
    {
        if (IsCapturing) return;

        IsCapturing = true;
        StatusText = "Muestreando actividad de CPU, RAM e I/O de disco en tiempo real...";

        try
        {
            var snap = await _diagnosticService.CaptureSnapshotAsync();
            Snapshot = snap;

            Bottlenecks.Clear();
            foreach (var b in snap.Bottlenecks)
            {
                Bottlenecks.Add(b);
            }
            OnPropertyChanged(nameof(HasBottlenecks));
            OnPropertyChanged(nameof(HasNoBottlenecks));

            UpdateProcessesList();

            StatusText = $"Diagnóstico capturado a las {snap.CapturedAt.ToLocalTime():T}. " +
                         (snap.HasBottlenecks 
                            ? $"Se detectaron {snap.Bottlenecks.Count} cuellos de botella activos." 
                            : "Uso general de recursos equilibrado sin saturación.");
        }
        catch (Exception ex)
        {
            StatusText = $"Error al capturar diagnóstico: {ex.Message}";
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    public async Task TerminateProcessAsync(ProcessResourceInfo? process)
    {
        if (process == null || process.IsSystemProcess) return;

        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Finalizar proceso",
            $"¿Estás seguro de que deseas forzar el cierre de '{process.ProcessName}' (PID {process.Pid})?\n" +
            (string.IsNullOrWhiteSpace(process.WindowTitle) ? "" : $"Ventana: {process.WindowTitle}\n") +
            "Los datos no guardados en esta aplicación podrían perderse.",
            "Finalizar Proceso",
            "Cancelar");

        if (!confirm) return;

        bool success = await _diagnosticService.TerminateProcessAsync(process.Pid);
        if (success)
        {
            StatusText = $"Proceso '{process.ProcessName}' (PID {process.Pid}) finalizado con éxito. Recapturando...";
            await CaptureAsync();
        }
        else
        {
            StatusText = $"No se pudo finalizar el proceso '{process.ProcessName}'. Puede requerir permisos de Administrador.";
        }
    }

    [RelayCommand]
    public static void OpenTaskManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "taskmgr.exe", UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    public static void OpenResourceMonitor()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "resmon.exe", UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    public async Task TerminateGroupAsync(AppProcessGroup? group)
    {
        if (group == null || group.IsSystemProcess) return;

        string title = group.HasMultipleProcesses ? $"Finalizar aplicación ({group.ProcessCount} procesos)" : "Finalizar proceso";
        string msg = group.HasMultipleProcesses
            ? $"¿Estás seguro de que deseas forzar el cierre de todas las instancias ({group.ProcessCount}) de '{group.ProcessName}'?\n" +
              (string.IsNullOrWhiteSpace(group.MainWindowTitle) ? "" : $"Ventana: {group.MainWindowTitle}\n") +
              "Se finalizarán todos los subprocesos de esta aplicación."
            : $"¿Estás seguro de que deseas forzar el cierre de '{group.ProcessName}' (PID {group.SubProcesses[0].Pid})?\n" +
              (string.IsNullOrWhiteSpace(group.MainWindowTitle) ? "" : $"Ventana: {group.MainWindowTitle}\n");

        bool confirm = await _dialogService.ShowConfirmationAsync(
            title,
            msg,
            group.HasMultipleProcesses ? "Finalizar Todo" : "Finalizar Proceso",
            "Cancelar");

        if (!confirm) return;

        int closed = 0;
        foreach (var proc in group.SubProcesses)
        {
            if (!proc.IsSystemProcess && await _diagnosticService.TerminateProcessAsync(proc.Pid))
            {
                closed++;
            }
        }

        StatusText = $"Se cerraron {closed} de {group.ProcessCount} instancias de '{group.ProcessName}'. Recapturando diagnóstico...";
        await CaptureAsync();
    }

    private void UpdateProcessesList()
    {
        ProcessGroups.Clear();
        Processes.Clear();
        if (Snapshot == null) return;

        var groupSource = SelectedTab switch
        {
            1 => Snapshot.TopMemoryGroups,
            2 => Snapshot.TopDiskGroups,
            _ => Snapshot.TopCpuGroups
        };

        foreach (var g in groupSource)
        {
            ProcessGroups.Add(g);
        }

        var source = SelectedTab switch
        {
            1 => Snapshot.TopMemoryProcesses,
            2 => Snapshot.TopDiskProcesses,
            _ => Snapshot.TopCpuProcesses
        };

        foreach (var p in source)
        {
            Processes.Add(p);
        }
    }
}
