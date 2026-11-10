using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Abstractions;

public interface IPreferencesService
{
    Preset CurrentPreset { get; set; }
    Preset GetPresetForCategory(Category category);
    void SetPresetForCategory(Category category, Preset preset);

    bool CreateRestorePoint { get; set; }
    event EventHandler? PresetChanged;
    event EventHandler<Category>? CategoryPresetChanged;
}
