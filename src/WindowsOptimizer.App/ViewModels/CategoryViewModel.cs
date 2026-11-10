using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;

namespace WindowsOptimizer_App.ViewModels;

/// <summary>
/// ViewModel genérico para páginas filtradas por categoría.
/// Cada módulo tiene su propio selector de preset independiente.
/// </summary>
public sealed partial class CategoryViewModel : ObservableObject
{
    private readonly ScanOrchestrator _scanOrchestrator;
    private readonly FixOrchestrator _fixOrchestrator;
    private readonly IPreferencesService _preferencesService;
    private readonly IDialogService _dialogService;
    private readonly ISystemRestoreService _restoreService;
    private readonly INotificationService _notificationService;
    private readonly Category _category;

    // Último resultado de escaneo completo cacheado para re-filtrar sin re-escanear
    private ScanResult? _lastScanResult;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
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

    private int _appliedCount;
    public int AppliedCount
    {
        get => _appliedCount;
        set => SetProperty(ref _appliedCount, value);
    }

    private int _optimalCount;
    public int OptimalCount
    {
        get => _optimalCount;
        set => SetProperty(ref _optimalCount, value);
    }

    public ObservableCollection<Finding> Findings { get; } = new();

    public string CategoryName { get; }

    /// <summary>Presets disponibles para el selector del módulo.</summary>
    public IReadOnlyList<Models.PresetItem> AvailablePresets { get; } = Models.PresetItem.All;

    /// <summary>Preset seleccionado actualmente para esta categoría.</summary>
    private Models.PresetItem? _selectedPresetItem;
    public Models.PresetItem? SelectedPresetItem
    {
        get => _selectedPresetItem;
        set
        {
            if (SetProperty(ref _selectedPresetItem, value) && value != null)
            {
                _preferencesService.SetPresetForCategory(_category, value.Value);
                // Re-filtrar con el nuevo preset sin volver a escanear
                if (_lastScanResult != null)
                {
                    ApplyFilter(_lastScanResult);
                }
            }
        }
    }

    /// <summary>Preset enum actual de esta categoría.</summary>
    public Preset CurrentCategoryPreset => _preferencesService.GetPresetForCategory(_category);

    public CategoryViewModel(
        ScanOrchestrator scanOrchestrator,
        FixOrchestrator fixOrchestrator,
        IPreferencesService preferencesService,
        IDialogService dialogService,
        ISystemRestoreService restoreService,
        INotificationService notificationService,
        Category category,
        string categoryName)
    {
        _scanOrchestrator = scanOrchestrator;
        _fixOrchestrator = fixOrchestrator;
        _preferencesService = preferencesService;
        _dialogService = dialogService;
        _restoreService = restoreService;
        _notificationService = notificationService;
        _category = category;
        CategoryName = categoryName;
        StatusMessage = $"Pulsa 'Escanear' para analizar {categoryName.ToLowerInvariant()}.";

        // Inicializar el selector al preset actual de la categoría
        var currentPreset = _preferencesService.GetPresetForCategory(_category);
        _selectedPresetItem = AvailablePresets.FirstOrDefault(p => p.Value == currentPreset)
            ?? AvailablePresets.FirstOrDefault(p => p.Value == Preset.Standard);
    }

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Escaneando...";
        Findings.Clear();

        try
        {
            _lastScanResult = await _scanOrchestrator.RunFullScanAsync();
            ApplyFilter(_lastScanResult);
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

    /// <summary>
    /// Filtra los hallazgos del último escaneo según la categoría y el preset seleccionado.
    /// </summary>
    private void ApplyFilter(ScanResult result)
    {
        Findings.Clear();
        var preset = CurrentCategoryPreset;

        var categoryFindings = result.Findings
            .Where(f => f.Category == _category && (f.Presets == null || f.Presets.Count == 0 || f.Presets.Contains(preset)))
            .OrderByDescending(f => f.Severity);

        foreach (var finding in categoryFindings)
        {
            Findings.Add(finding);
        }

        RefreshCounters();
        StatusMessage = $"{Findings.Count} reglas en {preset} · {DetectedCount} pendientes · {OptimalCount + AppliedCount} óptimos";
    }

    [RelayCommand]
    private async Task ApplyFixAsync(Finding? finding)
    {
        if (finding is null || (finding.Status != FindingStatus.Detected && finding.Status != FindingStatus.Drifted)) return;

        try
        {
            // Mostrar toast de progreso (sin modal de confirmación individual)
            _notificationService.ShowProgress(
                "Aplicando optimización",
                finding.Name);

            if (_preferencesService.CreateRestorePoint)
            {
                StatusMessage = "Creando punto de restauración...";
                await _restoreService.CreateRestorePointAsync($"Windows Optimizer - {CategoryName}");
            }

            await _fixOrchestrator.ApplyFixAsync(finding);

            // Toast de éxito
            _notificationService.ShowSuccess(
                "Optimización aplicada",
                $"{finding.Name} aplicada correctamente.");

            StatusMessage = $"✓ Aplicado: {finding.Name}";

            // Re-escanear para actualizar el estado de las tarjetas
            await ScanAsync();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                "Error al aplicar",
                ex.Message);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyAllAsync()
    {
        var detected = Findings.Where(f => (f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted) && f.IsSelected).ToList();
        if (detected.Count == 0) return;

        bool confirm = await _dialogService.ShowConfirmationAsync(
            $"Confirmar aplicación en {CategoryName}",
            $"¿Estás seguro de que deseas aplicar {detected.Count} optimizaciones?",
            "Aplicar",
            "Cancelar");

        if (!confirm) return;

        // Toast de progreso en batch
        _notificationService.ShowProgress(
            "Aplicando optimizaciones",
            $"Aplicando {detected.Count} cambios...");

        if (_preferencesService.CreateRestorePoint)
        {
            StatusMessage = "Creando punto de restauración...";
            await _restoreService.CreateRestorePointAsync($"Windows Optimizer - {CategoryName}");
        }

        int applied = 0;

        foreach (var f in detected)
        {
            try
            {
                await _fixOrchestrator.ApplyFixAsync(f);
                applied++;
            }
            catch { }
        }

        // Toast de éxito en batch
        _notificationService.ShowSuccess(
            "Optimizaciones aplicadas",
            $"{applied}/{detected.Count} cambios aplicados correctamente.");

        StatusMessage = $"✓ {applied}/{detected.Count} correcciones aplicadas.";

        // Re-escanear para actualizar el estado de las tarjetas
        await ScanAsync();
    }

    private void RefreshCounters()
    {
        DetectedCount = Findings.Count(f => f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted);
        AppliedCount = Findings.Count(f => f.Status == FindingStatus.Applied);
        OptimalCount = Findings.Count(f => f.Status == FindingStatus.AlreadyOptimal);
    }

    private void RefreshList(Finding finding)
    {
        var idx = Findings.IndexOf(finding);
        if (idx >= 0)
        {
            Findings.RemoveAt(idx);
            Findings.Insert(idx, finding);
        }
    }
}
