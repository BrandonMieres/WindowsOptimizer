using CommunityToolkit.Mvvm.ComponentModel;
using System.ServiceProcess;

namespace WindowsOptimizer_App.Models;

public partial class WindowsServiceItem : ObservableObject
{
    [ObservableProperty]
    private string _serviceName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _startType = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private bool _isMicrosoft;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _dependencies = string.Empty;
}