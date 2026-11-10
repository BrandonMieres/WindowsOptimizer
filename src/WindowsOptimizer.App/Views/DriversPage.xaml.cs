using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class DriversPage : Page
{
    public DriversViewModel ViewModel { get; }

    public DriversPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DriversViewModel>();
        DataContext = ViewModel;
    }
}
