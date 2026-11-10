using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Platform;
using WindowsOptimizer.Rules;
using WindowsOptimizer.Scanners;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer_App.ViewModels;
using WindowsOptimizer_App.Services;
using System.IO;

namespace WindowsOptimizer_App;

public partial class App : Application
{
    private Window? _window;
    public static Window? MainWindow { get; private set; }
    
    public IServiceProvider Services { get; }

    public new static App Current => (App)Application.Current;

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
        UnhandledException += (sender, e) =>
        {
            string crashLog = System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log");
            string msg = $"[{DateTime.Now}] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n";
            if (e.Exception.InnerException != null)
                msg += $"Inner: {e.Exception.InnerException.GetType().Name}: {e.Exception.InnerException.Message}\n{e.Exception.InnerException.StackTrace}\n\n";
            System.IO.File.AppendAllText(crashLog, msg);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuración de rutas
        string appDir = AppContext.BaseDirectory;
        string rulesDir = System.IO.Path.Combine(appDir, "rules");

        // Servicios del Core y Platform
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IDiskAnalyzerService, DiskAnalyzerService>();
        services.AddSingleton<IRuleProvider>(new JsonRuleProvider(rulesDir));
        services.AddSingleton<IRegistryAccess, RegistryAccess>();
        services.AddSingleton<IServiceManager, WindowsServiceManager>();
        services.AddSingleton<IPreferencesService, PreferencesService>();
        services.AddSingleton<IAppxService, WindowsOptimizer.Platform.AppxService>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<IDriveMediaService, DriveMediaService>();
        services.AddSingleton<IPowerPlanService, PowerPlanService>();
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<ISystemBaselineService, WindowsOptimizer.Measurement.SystemBaselineService>();
        services.AddSingleton<IImpactEstimator, WindowsOptimizer.Measurement.ImpactEstimator>();
        services.AddSingleton<IDeviceHealthService, DeviceHealthService>();
        services.AddSingleton<ILiveDiagnosticService, LiveDiagnosticService>();

        // Rollback store
        string rollbackPath = System.IO.Path.Combine(appDir, "data", "rollback.json");
        services.AddSingleton<IRollbackStore>(new JsonRollbackStore(rollbackPath));
        
        // Scanners — uno por categoría para que el filtrado sea correcto
        services.AddTransient<IScanner>(sp => new RegistryScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IRegistryAccess>(),
            Category.Privacy));

        services.AddTransient<IScanner>(sp => new ScheduledTaskScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IScheduledTaskService>(),
            Category.Privacy));

        services.AddTransient<IScanner>(sp => new RegistryScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IRegistryAccess>(),
            Category.Startup));

        services.AddTransient<IScanner>(sp => new RegistryScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IRegistryAccess>(),
            Category.Network));

        services.AddTransient<IScanner>(sp => new RegistryScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IRegistryAccess>(),
            Category.Disk));

        services.AddTransient<IScanner>(sp => new ServiceScanner(
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IServiceManager>(),
            sp.GetRequiredService<IDriveMediaService>()));
        services.AddTransient<IScanner, CleanerScanner>();
        services.AddTransient<IScanner, AppxScanner>();
        services.AddTransient<IScanner>(sp => new RegistryScanner(sp.GetRequiredService<IRuleProvider>(), sp.GetRequiredService<IRegistryAccess>(), Category.Performance));
        services.AddTransient<IScanner>(sp => new RegistryScanner(sp.GetRequiredService<IRuleProvider>(), sp.GetRequiredService<IRegistryAccess>(), Category.Gaming));
        services.AddTransient<IScanner>(sp => new PowerPlanScanner(sp.GetRequiredService<IPowerPlanService>(), sp.GetRequiredService<IRuleProvider>(), Category.Gaming));
        services.AddTransient<IScanner>(sp => new PowerPlanScanner(sp.GetRequiredService<IPowerPlanService>(), sp.GetRequiredService<IRuleProvider>(), Category.Performance));
        services.AddTransient<IScanner>(sp => new RegistryScanner(sp.GetRequiredService<IRuleProvider>(), sp.GetRequiredService<IRegistryAccess>(), Category.System));
        services.AddTransient<IScanner>(sp => new RegistryScanner(sp.GetRequiredService<IRuleProvider>(), sp.GetRequiredService<IRegistryAccess>(), Category.Ui));
        services.AddSingleton<ScanOrchestrator>();
        services.AddSingleton(sp => new FixOrchestrator(
            sp.GetRequiredService<IRollbackStore>(),
            sp.GetRequiredService<IRegistryAccess>(),
            sp.GetRequiredService<IServiceManager>(),
            sp.GetRequiredService<IRuleProvider>(),
            sp.GetRequiredService<IAppxService>(),
            sp.GetRequiredService<IPowerPlanService>(),
            sp.GetRequiredService<IScheduledTaskService>()));

        // ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<FindingsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<DiskViewModel>();
        services.AddTransient<SystemInfoViewModel>();
        services.AddTransient<DriversViewModel>();
        services.AddTransient<LiveDiagnosticViewModel>();
        services.AddKeyedTransient("Apps", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Apps,
            "Bloatware"));

        services.AddTransient(sp => new AppsViewModel(
            sp.GetRequiredKeyedService<CategoryViewModel>("Apps"),
            sp.GetRequiredService<IAppxService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<IPreferencesService>()
        ));

        // Category ViewModels
        services.AddKeyedTransient("Privacy", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Privacy,
            "Privacidad"));

        services.AddKeyedTransient("Services", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Services,
            "Servicios"));

        services.AddTransient(sp => new ServicesViewModel(
            sp.GetRequiredKeyedService<CategoryViewModel>("Services")
        ));
        services.AddKeyedTransient("Gaming", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Gaming,
            "Gaming"));

        services.AddTransient(sp => new GamingViewModel(
            sp.GetRequiredKeyedService<CategoryViewModel>("Gaming"),
            sp.GetRequiredService<IPowerPlanService>()
        ));
        
        services.AddTransient<WindowsOptimizer_App.Views.GamingPage>();

        services.AddKeyedTransient("Cleaners", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Cleaners,
            "Limpieza"
        ));
        services.AddKeyedTransient("Performance", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Performance,
            "Rendimiento"
        ));

        services.AddKeyedTransient("Startup", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Startup,
            "Arranque"));

        services.AddKeyedTransient("Network", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Network,
            "Red e Internet"));

        services.AddTransient(sp => new NetworkViewModel(
            sp.GetRequiredKeyedService<CategoryViewModel>("Network")
        ));

        services.AddKeyedTransient("SystemTweaks", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.System,
            "Ajustes de Sistema"));

        services.AddKeyedTransient("Ui", (sp, key) => new CategoryViewModel(
            sp.GetRequiredService<ScanOrchestrator>(),
            sp.GetRequiredService<FixOrchestrator>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<ISystemRestoreService>(),
            sp.GetRequiredService<INotificationService>(),
            Category.Ui,
            "Personalización UI"));

        return services.BuildServiceProvider();
    }
}
