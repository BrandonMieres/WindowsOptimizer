using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class DriversViewModel : ObservableObject
{
    private readonly IDeviceHealthService _healthService;
    private readonly List<DriverItem> _allDrivers = new();

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(HasNoProblems));
            }
        }
    }

    private string _statusText = "Listo para auditar hardware";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private DeviceHealthSummary? _summary;
    public DeviceHealthSummary? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    private int _filterIndex; // 0 = Todos, 1 = Genéricos, 2 = Antiguos, 3 = Sin Firma
    public int FilterIndex
    {
        get => _filterIndex;
        set
        {
            if (SetProperty(ref _filterIndex, value))
            {
                ApplyFilter();
            }
        }
    }

    public ObservableCollection<DeviceProblem> Problems { get; } = new();
    public ObservableCollection<DriverItem> FilteredDrivers { get; } = new();

    public bool HasProblems => Problems.Count > 0;
    public bool HasNoProblems => !IsScanning && Problems.Count == 0 && Summary != null;

    public DriversViewModel(IDeviceHealthService healthService)
    {
        _healthService = healthService;
        _ = ScanAsync();
    }

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        StatusText = "Auditando dispositivos y controladores Plug and Play...";
        Problems.Clear();
        FilteredDrivers.Clear();
        _allDrivers.Clear();
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(HasNoProblems));

        try
        {
            var (problems, drivers, summary) = await _healthService.AuditDevicesAndDriversAsync();

            Summary = summary;
            _allDrivers.AddRange(drivers);

            foreach (var prob in problems)
            {
                Problems.Add(prob);
            }

            ApplyFilter();

            StatusText = summary.IsHealthy
                ? $"Auditoría completada: {drivers.Count} controladores analizados. Todo el hardware funciona correctamente."
                : $"Atención: {problems.Count} problema(s) detectado(s) en {drivers.Count} controladores analizados.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al auditar hardware: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasProblems));
            OnPropertyChanged(nameof(HasNoProblems));
        }
    }

    [RelayCommand]
    public static void OpenDeviceManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "devmgmt.msc",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silencioso o fallback
        }
    }

    private void ApplyFilter()
    {
        FilteredDrivers.Clear();

        IEnumerable<DriverItem> query = _allDrivers;

        if (FilterIndex == 1) // Genéricos
        {
            query = query.Where(d => d.IsGenericMicrosoftDriver);
        }
        else if (FilterIndex == 2) // Antiguos
        {
            query = query.Where(d => d.IsOld);
        }
        else if (FilterIndex == 3) // Sin firma
        {
            query = query.Where(d => !d.IsSigned);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string q = SearchQuery.Trim();
            query = query.Where(d =>
                d.DeviceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceClass.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.DriverProvider.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredDrivers.Add(item);
        }
    }
}
