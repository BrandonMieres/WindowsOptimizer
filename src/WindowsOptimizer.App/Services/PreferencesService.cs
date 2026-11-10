using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer_App.Services;

public sealed class PreferencesService : IPreferencesService
{
    private Preset _currentPreset = Preset.Standard;
    private readonly System.Collections.Generic.Dictionary<Category, Preset> _categoryPresets = new();

    public bool CreateRestorePoint { get; set; } = true;

    public Preset CurrentPreset
    {
        get => _currentPreset;
        set
        {
            if (_currentPreset != value)
            {
                _currentPreset = value;
                // Update all categories when the global preset changes
                foreach (var category in Enum.GetValues<Category>())
                {
                    _categoryPresets[category] = value;
                }
                PresetChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Preset GetPresetForCategory(Category category)
    {
        if (_categoryPresets.TryGetValue(category, out var preset))
            return preset;
        return _currentPreset;
    }

    public void SetPresetForCategory(Category category, Preset preset)
    {
        if (GetPresetForCategory(category) != preset)
        {
            _categoryPresets[category] = preset;
            CategoryPresetChanged?.Invoke(this, category);
        }
    }

    public event EventHandler? PresetChanged;
    public event EventHandler<Category>? CategoryPresetChanged;
}
