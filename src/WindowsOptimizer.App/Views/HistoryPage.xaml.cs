using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<HistoryViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadHistoryCommand.ExecuteAsync(null);
    }

    private async void RevertChange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ChangeRecord record)
        {
            await ViewModel.RevertChangeCommand.ExecuteAsync(record);
        }
    }
}