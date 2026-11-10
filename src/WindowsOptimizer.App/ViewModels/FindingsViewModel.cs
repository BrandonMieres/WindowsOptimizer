using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class FindingsViewModel : ObservableObject
{
    private readonly ScanOrchestrator _scanOrchestrator;
    private readonly FixOrchestrator _fixOrchestrator;
    private readonly INotificationService _notificationService;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = "Pulsa 'Escanear' para analizar el sistema.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _detectedCount;
    public int DetectedCount
    {
        get => _detectedCount;
        set => SetProperty(ref _detectedCount, value);
    }

    private int _fixedCount;
    public int FixedCount
    {
        get => _fixedCount;
        set => SetProperty(ref _fixedCount, value);
    }

    private int _optimalCount;
    public int OptimalCount
    {
        get => _optimalCount;
        set => SetProperty(ref _optimalCount, value);
    }

    public ObservableCollection<Finding> Findings { get; } = new();

    public FindingsViewModel(
        ScanOrchestrator scanOrchestrator,
        FixOrchestrator fixOrchestrator,
        INotificationService notificationService)
    {
        _scanOrchestrator = scanOrchestrator;
        _fixOrchestrator = fixOrchestrator;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Escaneando sistema...";
        Findings.Clear();

        try
        {
            var result = await _scanOrchestrator.RunFullScanAsync();

            foreach (var finding in result.Findings.OrderByDescending(f => f.Severity))
            {
                Findings.Add(finding);
            }

            DetectedCount = result.Findings.Count(f => f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted);
            FixedCount = result.Findings.Count(f => f.Status == FindingStatus.Applied);
            OptimalCount = result.Findings.Count(f => f.Status == FindingStatus.AlreadyOptimal);

            StatusMessage = $"{result.Findings.Count} hallazgos · {DetectedCount} pendientes · {OptimalCount} ya óptimos";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplyFixAsync(Finding? finding)
    {
        if (finding is null || (finding.Status != FindingStatus.Detected && finding.Status != FindingStatus.Drifted)) return;

        try
        {
            _notificationService.ShowProgress(
                "Aplicando optimización",
                finding.Name);

            await _fixOrchestrator.ApplyFixAsync(finding);

            _notificationService.ShowSuccess(
                "Optimización aplicada",
                $"{finding.Name} aplicada correctamente.");

            StatusMessage = $"✓ Aplicado: {finding.Name}";

            // Re-escanear para actualizar el estado de los hallazgos
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Error al aplicar",
                ex.Message);
            StatusMessage = $"Error al aplicar {finding.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyAllDetectedAsync()
    {
        var detected = Findings.Where(f => f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted).ToList();
        if (detected.Count == 0) return;

        _notificationService.ShowProgress(
            "Aplicando optimizaciones",
            $"Aplicando {detected.Count} cambios...");

        int applied = 0;

        foreach (var finding in detected)
        {
            try
            {
                await _fixOrchestrator.ApplyFixAsync(finding);
                applied++;
            }
            catch
            {
                // Continuamos con las demás
            }
        }

        _notificationService.ShowSuccess(
            "Optimizaciones aplicadas",
            $"{applied}/{detected.Count} cambios aplicados correctamente.");

        StatusMessage = $"✓ {applied}/{detected.Count} correcciones aplicadas.";

        // Re-escanear para actualizar la lista
        await ScanAsync();
    }
}
