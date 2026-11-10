using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        DataContext = ViewModel;
        UpdatePresetInfoBar(ViewModel.CurrentPreset);

        // Listen for findings changes to show/hide the section
        ViewModel.RecentFindings.CollectionChanged += (s, e) =>
        {
            UpdateFindingsSection();
        };
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is Models.PresetItem presetItem)
        {
            UpdatePresetInfoBar(presetItem.Value);
        }
    }

    private void UpdatePresetInfoBar(Preset preset)
    {
        // Guard: controls may not be loaded yet during init
        if (PresetInfoText is null) return;

        switch (preset)
        {
            case Preset.Minimal:
                PresetInfoIcon.Glyph = "\uE8D7";  // Shield
                PresetInfoText.Text = "Solo cambios seguros y conservadores. Ideal si prefieres no tocar nada que pueda afectar al funcionamiento.";
                PresetBadgeText.Text = "Seguro";
                PresetBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 142, 60)); // green
                break;

            case Preset.Standard:
                PresetInfoIcon.Glyph = "\uE946";  // Info
                PresetInfoText.Text = "Análisis equilibrado con las optimizaciones recomendadas para la mayoría de usuarios.";
                PresetBadgeText.Text = "\u2713 Recomendado";
                PresetBadge.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                break;

            case Preset.Advanced:
                PresetInfoIcon.Glyph = "\uE7BA";  // Warning
                PresetInfoText.Text = "Incluye todas las optimizaciones, algunas pueden afectar a funcionalidades del sistema. Solo para usuarios avanzados.";
                PresetBadgeText.Text = "\u26A0 Avanzado";
                PresetBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 202, 81, 0)); // amber/orange
                break;
        }
    }

        private void UpdateFindingsSection()
    {
        var count = ViewModel.RecentFindings.Count;
        if (count > 0)
        {
            FindingsSection.Visibility = Visibility.Visible;
            int detected = ViewModel.RecentFindings.Count(f => f.Status == FindingStatus.Detected);
            int drifted = ViewModel.RecentFindings.Count(f => f.Status == FindingStatus.Drifted);
            int optimal = ViewModel.RecentFindings.Count(f => f.Status == FindingStatus.AlreadyOptimal || f.Status == FindingStatus.Applied);
            FindingsCountText.Text = $" {count} hallazgos encontrados en el último análisis";
            DetectedCountText.Text = $" {detected} pendientes";
            OptimalCountText.Text = $" {optimal} ya óptimos";
            if (drifted > 0)
            {
                DriftedCounterBorder.Visibility = Visibility.Visible;
                DriftedCountText.Text = $" {drifted} revertidos (Windows Update)";
            }
            else
            {
                DriftedCounterBorder.Visibility = Visibility.Collapsed;
            }
            OptimizeAllButton.IsEnabled = (detected + drifted) > 0;
        }
        else
        {
            FindingsSection.Visibility = Visibility.Collapsed;
        }
    }

    private void FindingCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WindowsOptimizer.Core.Models.Finding finding)
        {
            finding.IsSelected = !finding.IsSelected;
        }
    }
}

