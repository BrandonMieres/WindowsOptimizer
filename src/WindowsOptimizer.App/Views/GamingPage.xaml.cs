using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class GamingPage : Page
{
    public GamingViewModel ViewModel { get; }

    public GamingPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<GamingViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Tweaks.Findings.Count == 0)
        {
            await ViewModel.Tweaks.ScanCommand.ExecuteAsync(null);
        }
        if (ViewModel.PowerPlans.Count == 0)
        {
            await ViewModel.LoadPowerPlansCommand.ExecuteAsync(null);
        }
    }

    private async void PowerPlan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PowerPlanItem planItem)
        {
            await ViewModel.ActivatePowerPlanCommand.ExecuteAsync(planItem);
        }
    }

    private async void DeletePlan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PowerPlanItem planItem)
        {
            await ViewModel.DeletePlanCommand.ExecuteAsync(planItem);
        }
    }

    private void FindingCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Finding finding)
        {
            finding.IsSelected = !finding.IsSelected;
        }
    }
}
