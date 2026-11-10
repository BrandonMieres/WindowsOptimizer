using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsOptimizer_App.ViewModels;

public partial class DnsProfile : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _primaryAddress = string.Empty;

    [ObservableProperty]
    private string _secondaryAddress = string.Empty;

    [ObservableProperty]
    private long _latencyMs;

    [ObservableProperty]
    private bool _isTesting;

    public string LatencyText => LatencyMs < 0 ? "timeout" : LatencyMs == 0 ? "-" : $"{LatencyMs} ms";

    partial void OnLatencyMsChanged(long value) => OnPropertyChanged(nameof(LatencyText));
    partial void OnIsTestingChanged(bool value) => OnPropertyChanged(nameof(LatencyText));
}

public partial class NetworkViewModel : ObservableObject
{
    private readonly CategoryViewModel _categoryViewModel;
    public CategoryViewModel Tweaks => _categoryViewModel;

    [ObservableProperty]
    private ObservableCollection<DnsProfile> _dnsProfiles = new();

    [ObservableProperty]
    private DnsProfile? _selectedDnsProfile;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public NetworkViewModel(CategoryViewModel categoryViewModel)
    {
        _categoryViewModel = categoryViewModel;
        
        // Initialize DNS Profiles
        DnsProfiles.Add(new DnsProfile { Name = "Cloudflare", PrimaryAddress = "1.1.1.1", SecondaryAddress = "1.0.0.1" });
        DnsProfiles.Add(new DnsProfile { Name = "Google", PrimaryAddress = "8.8.8.8", SecondaryAddress = "8.8.4.4" });
        DnsProfiles.Add(new DnsProfile { Name = "Quad9", PrimaryAddress = "9.9.9.9", SecondaryAddress = "149.112.112.112" });
        DnsProfiles.Add(new DnsProfile { Name = "OpenDNS", PrimaryAddress = "208.67.222.222", SecondaryAddress = "208.67.220.220" });
    }

    [RelayCommand]
    private static async Task TestLatencyAsync(DnsProfile profile)
    {
        if (profile == null) return;
        
        profile.IsTesting = true;
        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(profile.PrimaryAddress, 3000);
            if (reply.Status == IPStatus.Success)
            {
                profile.LatencyMs = reply.RoundtripTime;
            }
            else
            {
                profile.LatencyMs = -1; // -1 for timeout
            }
        }
        catch
        {
            profile.LatencyMs = -1;
        }
        finally
        {
            profile.IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task TestAllLatencyAsync()
    {
        foreach (var profile in DnsProfiles)
        {
            await TestLatencyAsync(profile);
        }
    }

    [RelayCommand]
    private async Task ApplyDnsAsync()
    {
        if (SelectedDnsProfile == null) return;

        StatusMessage = $"Aplicando {SelectedDnsProfile.Name} DNS...";
        
        await Task.Run(() => 
        {
            var psArgs = $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }} | Set-DnsClientServerAddress -ServerAddresses '{SelectedDnsProfile.PrimaryAddress}', '{SelectedDnsProfile.SecondaryAddress}'\"";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = psArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit();
        });

        StatusMessage = $"¡DNS cambiado a {SelectedDnsProfile.Name} con éxito!";
    }

    [RelayCommand]
    private async Task FlushDnsAsync()
    {
        StatusMessage = "Limpiando caché DNS...";
        
        await Task.Run(() => 
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = "/c ipconfig /flushdns",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit();
        });
        
        StatusMessage = "¡Caché DNS limpiada con éxito!";
    }
}

