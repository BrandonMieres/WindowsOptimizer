using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class NetworkPage : Page
{
    public CategoryViewModel ViewModel { get; }

    private readonly NetworkViewModel _networkVm;

    private readonly (string Name, string Primary, string Secondary)[] _dnsProfiles =
    [
        ("Cloudflare", "1.1.1.1", "1.0.0.1"),
        ("Google", "8.8.8.8", "8.8.4.4"),
        ("Quad9", "9.9.9.9", "149.112.112.112"),
        ("OpenDNS", "208.67.222.222", "208.67.220.220"),
    ];

    public NetworkPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredKeyedService<CategoryViewModel>("Network");
        _networkVm = App.Current.Services.GetRequiredService<NetworkViewModel>();
        BuildDnsUI();
    }

    private void BuildDnsUI()
    {
        foreach (var (name, primary, _) in _dnsProfiles)
        {
            DnsComboBox.Items.Add(name);

            var card = new Border
            {
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8),
                MinWidth = 150,
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = name, FontWeight = new Windows.UI.Text.FontWeight(600) },
                        new TextBlock { Text = primary, FontSize = 12, Opacity = 0.7 },
                        new TextBlock { Name = $"Latency_{name}", Text = "-", FontWeight = new Windows.UI.Text.FontWeight(700) }
                    }
                }
            };
            if (Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out object brushRes) && brushRes is Microsoft.UI.Xaml.Media.Brush brush)
            {
                card.Background = brush;
            }
            DnsCardsPanel.Children.Add(card);
        }
        DnsComboBox.SelectedIndex = 0;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.ScanCommand.ExecuteAsync(null);
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ScanCommand.ExecuteAsync(null);

    private async void ApplyButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ApplyAllCommand.ExecuteAsync(null);

    private async void TestAll_Click(object sender, RoutedEventArgs e)
    {
        for (int i = 0; i < _dnsProfiles.Length; i++)
        {
            var (name, primary, _) = _dnsProfiles[i];
            if (DnsCardsPanel.Children[i] is Border card &&
                card.Child is StackPanel sp &&
                sp.Children[2] is TextBlock tb)
            {
                tb.Text = "probando...";
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(primary, 3000);
                    tb.Text = reply.Status == IPStatus.Success ? $"{reply.RoundtripTime} ms" : "timeout";
                }
                catch { tb.Text = "error"; }
            }
        }
    }

    private async void ApplyDns_Click(object sender, RoutedEventArgs e)
    {
        int idx = DnsComboBox.SelectedIndex;
        if (idx < 0) return;
        var (name, primary, secondary) = _dnsProfiles[idx];

        DnsStatusText.Text = $"Aplicando {name}...";
        DnsStatusText.Visibility = Visibility.Visible;

        await System.Threading.Tasks.Task.Run(() =>
        {
            var args = $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }} | Set-DnsClientServerAddress -ServerAddresses '{primary}', '{secondary}'\"";
            var p = new Process { StartInfo = new ProcessStartInfo("powershell", args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden } };
            p.Start(); p.WaitForExit();
        });

        DnsStatusText.Text = $"✓ DNS cambiado a {name}";
    }

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        DnsStatusText.Text = "Limpiando caché DNS...";
        DnsStatusText.Visibility = Visibility.Visible;
        await System.Threading.Tasks.Task.Run(() =>
        {
            var p = new Process { StartInfo = new ProcessStartInfo("cmd", "/c ipconfig /flushdns") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden } };
            p.Start(); p.WaitForExit();
        });
        DnsStatusText.Text = "✓ Caché DNS limpiada";
    }

    private void FindingCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Finding finding)
        {
            finding.IsSelected = !finding.IsSelected;
        }
    }
}
