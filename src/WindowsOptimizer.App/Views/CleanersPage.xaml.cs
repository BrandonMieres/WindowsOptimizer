using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class CleanersPage : Page
{
    public CategoryViewModel ViewModel { get; }

    public CleanersPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredKeyedService<CategoryViewModel>("Cleaners");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.ScanCommand.ExecuteAsync(null);
    }

    private async void ApplyFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Finding finding)
        {
            await ViewModel.ApplyFixCommand.ExecuteAsync(finding);
        }
    }
    private void FindingCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Finding finding)
        {
            finding.IsSelected = !finding.IsSelected;
        }
    }
}
