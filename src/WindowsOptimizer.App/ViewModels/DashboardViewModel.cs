using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly ScanOrchestrator _orchestrator;
    private readonly FixOrchestrator _fixOrchestrator;
    private readonly IPreferencesService _preferencesService;
    private readonly ISystemRestoreService _restoreService;
    private readonly IDialogService _dialogService;
    private readonly ISystemBaselineService _baselineService;
    private readonly IImpactEstimator _impactEstimator;

    private int _healthScore;
    public int HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private string _scanStatus = "Listo para escanear";
    public string ScanStatus
    {
        get => _scanStatus;
        set => SetProperty(ref _scanStatus, value);
    }

    private SystemBaseline? _baseline;
    public SystemBaseline? Baseline
    {
        get => _baseline;
        set
        {
            if (SetProperty(ref _baseline, value))
            {
                OnPropertyChanged(nameof(BootTimeDisplay));
                OnPropertyChanged(nameof(RamUsageDisplay));
                OnPropertyChanged(nameof(ProcessCountDisplay));
            }
        }
    }

    private AggregateImpactEstimate? _estimatedImpact;
    public AggregateImpactEstimate? EstimatedImpact
    {
        get => _estimatedImpact;
        set
        {
            if (SetProperty(ref _estimatedImpact, value))
            {
                OnPropertyChanged(nameof(ImpactSummaryDisplay));
                OnPropertyChanged(nameof(HasEstimatedImpact));
            }
        }
    }

    public bool HasEstimatedImpact => EstimatedImpact != null && EstimatedImpact.TotalOptimizationsCount > 0;
    public string ImpactSummaryDisplay => EstimatedImpact?.SummaryDescription ?? string.Empty;

    public string BootTimeDisplay
    {
        get
        {
            if (Baseline?.BootDuration.HasValue == true)
            {
                return $"{Baseline.BootDuration.Value.TotalSeconds:F1}s";
            }
            if (Baseline != null)
            {
                var up = Baseline.SystemUptime;
                return up.TotalHours >= 1 ? $"Activo {(int)up.TotalHours}h {up.Minutes}m" : $"Activo {up.Minutes}m";
            }
            return "Calculando...";
        }
    }

    public string RamUsageDisplay
    {
        get
        {
            if (Baseline != null && Baseline.TotalRamBytes > 0)
            {
                double usedGb = Baseline.UsedRamBytes / (1024.0 * 1024.0 * 1024.0);
                double totalGb = Baseline.TotalRamBytes / (1024.0 * 1024.0 * 1024.0);
                return $"{usedGb:F1} / {totalGb:F0} GB ({Baseline.RamUsagePercentage:F0}%)";
            }
            return "Calculando...";
        }
    }

    public string ProcessCountDisplay
    {
        get
        {
            if (Baseline != null && Baseline.ProcessCount > 0)
            {
                return $"{Baseline.ProcessCount} activos";
            }
            return "Calculando...";
        }
    }

    public WindowsOptimizer_App.Models.PresetItem SelectedPreset
    {
        get => WindowsOptimizer_App.Models.PresetItem.All.FirstOrDefault(p => p.Value == _preferencesService.CurrentPreset) ?? WindowsOptimizer_App.Models.PresetItem.All[1];
        set
        {
            if (value == null || _preferencesService.CurrentPreset == value.Value) return;
            _preferencesService.CurrentPreset = value.Value;
            OnPropertyChanged(nameof(SelectedPreset));
            OnPropertyChanged(nameof(CurrentPreset));
            _ = ScanAsync();
        }
    }

    public WindowsOptimizer.Core.Enums.Preset CurrentPreset => _preferencesService.CurrentPreset;

    public System.Collections.Generic.IReadOnlyList<WindowsOptimizer_App.Models.PresetItem> AvailablePresets { get; } = WindowsOptimizer_App.Models.PresetItem.All;

    public ObservableCollection<Finding> RecentFindings { get; } = new();

    public DashboardViewModel(
        ScanOrchestrator orchestrator,
        FixOrchestrator fixOrchestrator,
        IPreferencesService preferencesService,
        ISystemRestoreService restoreService,
        IDialogService dialogService,
        ISystemBaselineService baselineService,
        IImpactEstimator impactEstimator)
    {
        _orchestrator = orchestrator;
        _fixOrchestrator = fixOrchestrator;
        _preferencesService = preferencesService;
        _restoreService = restoreService;
        _dialogService = dialogService;
        _baselineService = baselineService;
        _impactEstimator = impactEstimator;

        // Cargar línea base inicial en segundo plano
        _ = Task.Run(async () =>
        {
            try
            {
                var baseline = await _baselineService.GetBaselineAsync();
                Baseline = baseline;
            }
            catch { }
        });
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanStatus = "Analizando sistema...";
        RecentFindings.Clear();

        try
        {
            var baselineTask = _baselineService.GetBaselineAsync();
            var scanTask = _orchestrator.RunFullScanAsync();
            await Task.WhenAll(baselineTask, scanTask);

            Baseline = await baselineTask;
            var result = await scanTask;

            var filteredFindings = result.Findings
                .Where(f => f.Presets == null || f.Presets.Count == 0 || f.Presets.Contains(_preferencesService.CurrentPreset))
                .ToList();

            EstimatedImpact = _impactEstimator.EstimateImpact(filteredFindings, Baseline);

            int score = 100;
            var pending = filteredFindings.Where(f => f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted);
            foreach (var f in pending)
            {
                score -= f.Severity switch
                {
                    Severity.Critical => 15,
                    Severity.High => 8,
                    Severity.Medium => 4,
                    Severity.Low => 2,
                    _ => 0
                };
            }
            HealthScore = Math.Max(0, score);
            ScanStatus = $"Último análisis: {result.ScannedAt.ToLocalTime():t} ({filteredFindings.Count} hallazgos en {CurrentPreset})";

            foreach (var finding in filteredFindings.OrderByDescending(f => f.Severity))
            {
                RecentFindings.Add(finding);
            }
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task ApplyPresetAsync()
    {
        var findingsToApply = RecentFindings.Where(f => f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted).ToList();
        if (findingsToApply.Count == 0) return;

        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Confirmar optimización",
            $"¿Estás seguro de que deseas aplicar el preset '{CurrentPreset}'? Se optimizarán {findingsToApply.Count} elementos.",
            "Aplicar Preset",
            "Cancelar");

        if (!confirm) return;

        IsScanning = true;
        try
        {
            if (_preferencesService.CreateRestorePoint)
            {
                ScanStatus = "Creando punto de restauración...";
                await _restoreService.CreateRestorePointAsync($"Windows Optimizer - Antes de {CurrentPreset}");
            }

            ScanStatus = $"Aplicando preset {CurrentPreset}...";
            var results = await _fixOrchestrator.ApplyPresetAsync(CurrentPreset, findingsToApply);
            
            ScanStatus = $"Se aplicaron {results.Count} correcciones exitosamente. Reescaneando...";
            IsScanning = false;
            await ScanAsync();
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error al aplicar preset: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task RevertAllAsync()
    {
        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Revertir optimizaciones",
            "¿Estás seguro de que deseas revertir TODAS las optimizaciones activas en el sistema?",
            "Revertir Todo",
            "Cancelar");

        if (!confirm) return;

        IsScanning = true;
        ScanStatus = "Revirtiendo optimizaciones...";

        try
        {
            await _fixOrchestrator.RevertAllAsync();
            ScanStatus = "Reversión completada. Reescaneando...";
            IsScanning = false;
            await ScanAsync();
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error al revertir: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
