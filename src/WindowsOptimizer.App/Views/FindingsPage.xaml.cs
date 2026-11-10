using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class FindingsPage : Page
{
    public FindingsViewModel ViewModel { get; }

    public FindingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<FindingsViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Findings.Count == 0)
        {
            await ViewModel.ScanCommand.ExecuteAsync(null);
        }
    }

    private async void ApplyFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Finding finding)
        {
            await ViewModel.ApplyFixCommand.ExecuteAsync(finding);
        }
    }
}