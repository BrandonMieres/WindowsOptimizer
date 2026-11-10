using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class LiveDiagnosticPage : Page
{
    public LiveDiagnosticViewModel ViewModel { get; }

    public LiveDiagnosticPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<LiveDiagnosticViewModel>();
        DataContext = ViewModel;
    }

    private void ToggleExpand_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement element && element.DataContext is AppProcessGroup group)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    private void CardHeader_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is AppProcessGroup group && group.HasMultipleProcesses)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    private async void TerminateGroupButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement element && element.DataContext is AppProcessGroup group)
        {
            await ViewModel.TerminateGroupAsync(group);
        }
    }

    private async void TerminateSubProcessButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement element && element.DataContext is ProcessResourceInfo process)
        {
            await ViewModel.TerminateProcessAsync(process);
        }
    }

    private async void TerminateButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ProcessResourceInfo process)
        {
            await ViewModel.TerminateProcessAsync(process);
        }
    }
}
