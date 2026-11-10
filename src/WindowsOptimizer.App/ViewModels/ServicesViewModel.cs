using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using WindowsOptimizer_App.Models;

namespace WindowsOptimizer_App.ViewModels;

public partial class ServicesViewModel : ObservableObject
{
    public CategoryViewModel Tweaks { get; }

    [ObservableProperty]
    private ObservableCollection<WindowsServiceItem> _servicesList = new();

    [ObservableProperty]
    private bool _isLoadingServices;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    private List<WindowsServiceItem> _allServices = new();

    public ServicesViewModel(CategoryViewModel categoryViewModel)
    {
        Tweaks = categoryViewModel;
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterServices();
    }

    [RelayCommand]
    private async Task LoadServicesAsync()
    {
        IsLoadingServices = true;
        _allServices.Clear();
        ServicesList.Clear();

        await Task.Run(() =>
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, PathName, Description FROM Win32_Service");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var path = obj["PathName"]?.ToString() ?? "";
                    var isMs = path.Contains("system32", System.StringComparison.OrdinalIgnoreCase) || 
                               path.Contains("svchost", System.StringComparison.OrdinalIgnoreCase) ||
                               path.Contains("Windows", System.StringComparison.OrdinalIgnoreCase);

                    var item = new WindowsServiceItem
                    {
                        ServiceName = obj["Name"]?.ToString() ?? "",
                        DisplayName = obj["DisplayName"]?.ToString() ?? "",
                        Status = obj["State"]?.ToString() ?? "",
                        StartType = obj["StartMode"]?.ToString() ?? "",
                        ExecutablePath = path,
                        Description = obj["Description"]?.ToString() ?? "",
                        IsMicrosoft = isMs
                    };
                    _allServices.Add(item);
                }
            }
            catch { }
        });

        FilterServices();
        IsLoadingServices = false;
    }

    private void FilterServices()
    {
        ServicesList.Clear();
        var query = SearchQuery;
        foreach (var svc in _allServices)
        {
            if (string.IsNullOrWhiteSpace(query) || 
                svc.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                svc.ServiceName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                ServicesList.Add(svc);
            }
        }
    }
}