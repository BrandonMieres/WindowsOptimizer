using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class AppsPage : Page
{
    public AppsViewModel ViewModel { get; }

    public AppsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<AppsViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Tweaks.Findings.Count == 0)
        {
            await ViewModel.Tweaks.ScanCommand.ExecuteAsync(null);
        }
        if (ViewModel.Apps.Count == 0)
        {
            await ViewModel.LoadAppsCommand.ExecuteAsync(null);
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
