using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer_App.Views;

namespace WindowsOptimizer_App;

/// <summary>
/// Ventana principal con shell de navegación categorizada.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        // Seleccionar Dashboard al iniciar
        NavView.SelectedItem = NavDashboard;
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();

            // Los contenedores de categorías padre no navegan, solo expanden/colapsan
            if (string.IsNullOrEmpty(tag) || tag.StartsWith("Cat_", StringComparison.Ordinal) || tag.StartsWith("Header_", StringComparison.Ordinal) || item.MenuItems.Count > 0)
            {
                return;
            }

            Type? pageType = tag switch
            {
                "Dashboard" => typeof(DashboardPage),
                "LiveDiagnostic" => typeof(LiveDiagnosticPage),
                "SystemInfo" => typeof(SystemInfoPage),
                "Findings" => typeof(FindingsPage),
                "Cleaners" => typeof(CleanersPage),
                "Services" => typeof(ServicesPage),
                "Privacy" => typeof(PrivacyPage),
                "Network" => typeof(NetworkPage),
                "Startup" => typeof(StartupPage),
                "Disk" => typeof(DiskPage),
                "Performance" => typeof(PerformancePage),
                "Gaming" => typeof(GamingPage),
                "Apps" => typeof(AppsPage),
                "SystemTweaks" => typeof(SystemTweaksPage),
                "Ui" => typeof(UiPage),
                "Drivers" => typeof(DriversPage),
                "History" => typeof(HistoryPage),
                _ => null
            };

            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    #region In-App Toast Notification

    private DispatcherTimer? _toastDismissTimer;

    /// <summary>
    /// Muestra el toast en modo progreso (spinner girando).
    /// </summary>
    public void ShowToastProgress(string title, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _toastDismissTimer?.Stop();
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastProgressRing.Visibility = Visibility.Visible;
            ToastProgressRing.IsActive = true;
            ToastSuccessBadge.Visibility = Visibility.Collapsed;
            ToastErrorBadge.Visibility = Visibility.Collapsed;
            NotificationToast.Visibility = Visibility.Visible;
        });
    }

    /// <summary>
    /// Transiciona el toast al estado de éxito y lo auto-descarta tras <paramref name="autoDismissMs"/> ms.
    /// </summary>
    public void ShowToastSuccess(string title, string message, int autoDismissMs = 3000)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastProgressRing.Visibility = Visibility.Collapsed;
            ToastProgressRing.IsActive = false;
            ToastSuccessBadge.Visibility = Visibility.Visible;
            ToastErrorBadge.Visibility = Visibility.Collapsed;
            NotificationToast.Visibility = Visibility.Visible;

            _toastDismissTimer?.Stop();
            _toastDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(autoDismissMs)
            };
            _toastDismissTimer.Tick += (s, e) =>
            {
                _toastDismissTimer.Stop();
                NotificationToast.Visibility = Visibility.Collapsed;
            };
            _toastDismissTimer.Start();
        });
    }

    /// <summary>
    /// Muestra el toast en estado de error y lo auto-descarta tras <paramref name="autoDismissMs"/> ms.
    /// </summary>
    public void ShowToastError(string title, string message, int autoDismissMs = 4000)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            ToastProgressRing.Visibility = Visibility.Collapsed;
            ToastProgressRing.IsActive = false;
            ToastSuccessBadge.Visibility = Visibility.Collapsed;
            ToastErrorBadge.Visibility = Visibility.Visible;
            NotificationToast.Visibility = Visibility.Visible;

            _toastDismissTimer?.Stop();
            _toastDismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(autoDismissMs)
            };
            _toastDismissTimer.Tick += (s, e) =>
            {
                _toastDismissTimer.Stop();
                NotificationToast.Visibility = Visibility.Collapsed;
            };
            _toastDismissTimer.Start();
        });
    }

    /// <summary>
    /// Oculta el toast inmediatamente.
    /// </summary>
    public void DismissToast()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _toastDismissTimer?.Stop();
            NotificationToast.Visibility = Visibility.Collapsed;
        });
    }

    private void CloseToast_Click(object sender, RoutedEventArgs e)
    {
        DismissToast();
    }

    #endregion
}
