using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer_App.ViewModels;

public partial class AppxPackageViewModel : ObservableObject
{
    private readonly AppxPackage _model;

    public AppxPackageViewModel(AppxPackage model)
    {
        _model = model;
    }

    public string Name => _model.Name;
    public string PackageFullName => _model.PackageFullName;
    public string Publisher => _model.Publisher;
    public string Version => _model.Version;
    public bool IsSystem => _model.IsSystem;

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class AppsViewModel : ObservableObject
{
    public CategoryViewModel Tweaks { get; }

    private readonly IAppxService _appxService;
    private readonly IDialogService _dialogService;
    private readonly ISystemRestoreService _restoreService;
    private readonly IPreferencesService _preferencesService;

    private List<AppxPackageViewModel> _allApps = new();

    [ObservableProperty]
    private ObservableCollection<AppxPackageViewModel> _apps = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Listo para escanear aplicaciones.";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _showSystemApps;

    partial void OnShowSystemAppsChanged(bool value)
    {
        FilterApps();
    }

    public AppsViewModel(
        CategoryViewModel categoryViewModel,
        IAppxService appxService,
        IDialogService dialogService,
        ISystemRestoreService restoreService,
        IPreferencesService preferencesService)
    {
        Tweaks = categoryViewModel;
        _appxService = appxService;
        _dialogService = dialogService;
        _restoreService = restoreService;
        _preferencesService = preferencesService;
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterApps();
    }

    private void FilterApps()
    {
        Apps.Clear();
        var query = SearchQuery;
        foreach (var app in _allApps)
        {
            if (!ShowSystemApps && app.IsSystem)
                continue;

            if (string.IsNullOrWhiteSpace(query) ||
                app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                Apps.Add(app);
            }
        }
    }

    [RelayCommand]
    private void SelectAllNonSystem()
    {
        foreach (var app in Apps)
        {
            if (!app.IsSystem)
            {
                app.IsSelected = true;
            }
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var app in Apps)
        {
            app.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task LoadAppsAsync()
    {
        IsLoading = true;
        StatusMessage = "Escaneando aplicaciones instaladas...";

        var packages = await _appxService.GetInstalledAppsAsync();
        
        // Excluir frameworks del sistema
        _allApps = packages
            .Where(p => !p.IsFramework)
            .OrderBy(p => p.Name)
            .Select(p => new AppxPackageViewModel(p))
            .ToList();

        FilterApps();

        IsLoading = false;
        int nonSystemCount = _allApps.Count(a => !a.IsSystem);
        int systemCount = _allApps.Count - nonSystemCount;
        StatusMessage = $"Encontradas {nonSystemCount} aplicaciones ({systemCount} componentes protegidos).";
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var toRemove = _allApps.Where(a => a.IsSelected && !a.IsSystem).ToList();

        if (toRemove.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Información", "Selecciona al menos una aplicación que no sea del sistema para desinstalar.");
            return;
        }

        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Confirmar Desinstalación",
            $"¿Estás seguro de que deseas desinstalar {toRemove.Count} aplicación(es)? Esta acción puede no ser reversible fácilmente sin descargarlas de nuevo de la Microsoft Store.");

        if (!confirm) return;

        if (_preferencesService.CreateRestorePoint)
        {
            StatusMessage = "Creando punto de restauración...";
            await _restoreService.CreateRestorePointAsync("Windows Optimizer - Desinstalación de Apps");
        }

        IsLoading = true;
        int successCount = 0;

        foreach (var app in toRemove)
        {
            StatusMessage = $"Desinstalando {app.Name}...";
            bool result = await _appxService.RemoveAppAsync(app.PackageFullName);
            if (result)
            {
                successCount++;
            }
        }

        IsLoading = false;
        await _dialogService.ShowMessageAsync("Proceso Completado", $"Se desinstalaron con éxito {successCount} de {toRemove.Count} aplicaciones.");
        
        await LoadAppsAsync();
        // También volvemos a escanear los tweaks recomendados para reflejar el nuevo estado
        await Tweaks.ScanCommand.ExecuteAsync(null);
    }
}
