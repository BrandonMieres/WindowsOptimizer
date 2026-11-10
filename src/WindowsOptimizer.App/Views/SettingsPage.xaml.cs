using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WindowsOptimizer_App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly WindowsOptimizer.Core.Abstractions.IPreferencesService _preferencesService;

    public SettingsPage()
    {
        InitializeComponent();
        _preferencesService = App.Current.Services.GetRequiredService<WindowsOptimizer.Core.Abstractions.IPreferencesService>();
        
        RestorePointToggle.IsOn = _preferencesService.CreateRestorePoint;
        RestorePointToggle.Toggled += RestorePointToggle_Toggled;
    }

    private void RestorePointToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _preferencesService.CreateRestorePoint = RestorePointToggle.IsOn;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedIndex < 0) return;

        var rootElement = this.XamlRoot?.Content as FrameworkElement;
        if (rootElement == null) return;

        rootElement.RequestedTheme = ThemeComboBox.SelectedIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Limpiar historial",
            Content = "Se eliminará todo el historial de cambios y escaneos. Esta acción no se puede deshacer.",
            PrimaryButtonText = "Limpiar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var rollbackStore = App.Current.Services.GetRequiredService<WindowsOptimizer.Core.Abstractions.IRollbackStore>();
            await rollbackStore.ClearAsync();
            
            // Optionally, show a confirmation dialog or toast
        }
    }
}