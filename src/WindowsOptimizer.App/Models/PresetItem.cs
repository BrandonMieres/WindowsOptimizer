using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer_App.Models;

/// <summary>
/// Modelo de presentación para cada nivel de análisis en el ComboBox.
/// </summary>
public sealed class PresetItem
{
    public required Preset Value { get; init; }
    public required string DisplayName { get; init; }
    public required string Icon { get; init; }
    public required string Description { get; init; }
    public string? Badge { get; init; }

    public static IReadOnlyList<PresetItem> All { get; } =
    [
        new PresetItem
        {
            Value = Preset.Minimal,
            DisplayName = "Básico",
            Icon = "\uE72E",       // Shield checkmark
            Description = "Solo optimizaciones seguras",
        },
        new PresetItem
        {
            Value = Preset.Standard,
            DisplayName = "Estándar",
            Icon = "\uE735",       // Favorite star
            Description = "Recomendado para todos",
            Badge = "Recomendado",
        },
        new PresetItem
        {
            Value = Preset.Advanced,
            DisplayName = "Avanzado",
            Icon = "\uE7BA",       // Warning
            Description = "Usuarios expertos",
        },
    ];

    public override string ToString() => DisplayName;
}
