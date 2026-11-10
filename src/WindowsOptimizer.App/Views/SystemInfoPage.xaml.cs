using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class SystemInfoPage : Page
{
    public SystemInfoViewModel ViewModel { get; }

    public SystemInfoPage()
    {
        ViewModel = ((WindowsOptimizer_App.App)Microsoft.UI.Xaml.Application.Current).Services.GetRequiredService<SystemInfoViewModel>();
        this.InitializeComponent();
        this.Loaded += SystemInfoPage_Loaded;
    }

    private void SystemInfoPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadSystemInfo(this.DispatcherQueue);
    }
}
