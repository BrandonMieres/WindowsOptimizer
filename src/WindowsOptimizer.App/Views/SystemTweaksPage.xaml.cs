using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class SystemTweaksPage : Page
{
    public CategoryViewModel ViewModel { get; }

    public SystemTweaksPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredKeyedService<CategoryViewModel>("SystemTweaks");
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.ScanCommand.ExecuteAsync(null);
    }

    private void FindingCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Finding finding)
        {
            finding.IsSelected = !finding.IsSelected;
        }
    }
}
