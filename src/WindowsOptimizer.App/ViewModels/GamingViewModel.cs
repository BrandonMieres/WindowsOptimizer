using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer_App.Models;

namespace WindowsOptimizer_App.ViewModels;

public partial class GamingViewModel : ObservableObject
{
    private readonly IPowerPlanService _powerPlanService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    public CategoryViewModel Tweaks { get; }

    [ObservableProperty]
    private ObservableCollection<PowerPlanItem> _powerPlans = new();

    [ObservableProperty]
    private bool _isLoadingPlans;

    [ObservableProperty]
    private string? _planStatusMessage;

    public GamingViewModel(CategoryViewModel categoryViewModel, IPowerPlanService powerPlanService)
    {
        Tweaks = categoryViewModel;
        _powerPlanService = powerPlanService;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    public async Task LoadPowerPlansAsync()
    {
        IsLoadingPlans = true;
        try
        {
            var plans = await _powerPlanService.GetPowerPlansAsync();
            _dispatcherQueue.TryEnqueue(() =>
            {
                PowerPlans.Clear();
                foreach (var p in plans)
                {
                    PowerPlans.Add(new PowerPlanItem
                    {
                        Guid = p.Guid,
                        Name = p.Name,
                        Description = p.Description,
                        IsActive = p.IsActive,
                        IsBuiltIn = p.IsBuiltIn,
                        SchemeType = p.SchemeType
                    });
                }
            });
        }
        finally
        {
            IsLoadingPlans = false;
        }
    }

    [RelayCommand]
    public async Task ActivatePowerPlanAsync(PowerPlanItem item)
    {
        if (item == null || item.IsActive) return;

        bool success = await _powerPlanService.SetActivePlanAsync(item.Guid);
        if (success)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                foreach (var plan in PowerPlans)
                {
                    plan.IsActive = (plan.Guid == item.Guid);
                }
            });
        }
        else
        {
            PlanStatusMessage = "No se pudo activar el plan seleccionado.";
        }

        await LoadPowerPlansAsync();
    }

    [RelayCommand]
    public async Task CreateUltimatePerformanceAsync()
    {
        IsLoadingPlans = true;
        try
        {
            var plan = await _powerPlanService.EnableUltimatePerformanceAsync();
            if (plan != null)
            {
                PlanStatusMessage = $"Plan '{plan.Name}' activado correctamente.";
            }
            await LoadPowerPlansAsync();
        }
        finally
        {
            IsLoadingPlans = false;
        }
    }

    [RelayCommand]
    public async Task DeletePlanAsync(PowerPlanItem item)
    {
        if (item == null || item.IsBuiltIn || item.IsActive) return;

        bool success = await _powerPlanService.DeletePlanAsync(item.Guid);
        if (success)
        {
            await LoadPowerPlansAsync();
        }
    }
}
