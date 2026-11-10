using CommunityToolkit.Mvvm.ComponentModel;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer_App.Models;

public partial class PowerPlanItem : ObservableObject
{
    [ObservableProperty]
    private string _guid = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isBuiltIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private PowerSchemeType _schemeType = PowerSchemeType.Unknown;

    public string IconGlyph => SchemeType switch
    {
        PowerSchemeType.UltimatePerformance => "\uE7E8", // Lightning
        PowerSchemeType.HighPerformance => "\uE7FC", // Speed / Performance
        PowerSchemeType.Balanced => "\uE8AB", // Balanced
        PowerSchemeType.PowerSaver => "\uEC0A", // Saver
        _ => "\uE770" // PC
    };

    public string BadgeText => SchemeType switch
    {
        PowerSchemeType.UltimatePerformance => "Máximo Rendimiento",
        PowerSchemeType.HighPerformance => "Alto Rendimiento",
        PowerSchemeType.Balanced => "Equilibrado",
        PowerSchemeType.PowerSaver => "Ahorro",
        _ => "Personalizado"
    };
}