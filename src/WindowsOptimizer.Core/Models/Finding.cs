using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Explicación de un hallazgo visible para el usuario.
/// Cada hallazgo explica qué es, por qué importa y qué cambia.
/// </summary>
public sealed record FindingExplanation
{
    /// <summary>Qué es este ajuste.</summary>
    public required string What { get; init; }

    /// <summary>Por qué importa cambiarlo.</summary>
    public required string Why { get; init; }

    /// <summary>Qué modifica exactamente.</summary>
    public required string Changes { get; init; }
}

/// <summary>
/// Hallazgo detectado por un scanner.
/// Representa un punto de mejora concreto en el sistema del usuario.
/// </summary>
public sealed partial class Finding : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    /// <summary>Identificador único del hallazgo (corresponde al id de la regla).</summary>
    public required string Id { get; init; }

    /// <summary>Nombre legible.</summary>
    public required string Name { get; init; }

    /// <summary>Descripción breve.</summary>
    public string? Description { get; init; }

    /// <summary>Categoría del hallazgo.</summary>
    public required Category Category { get; init; }

    /// <summary>Gravedad.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Nivel de riesgo del cambio propuesto.</summary>
    public required RiskLevel Risk { get; init; }

    private FindingStatus _status = FindingStatus.Detected;

    /// <summary>Estado actual del hallazgo.</summary>
    public FindingStatus Status 
    { 
        get => _status; 
        set 
        { 
            if (_status != value)
            {
                _status = value; 
                if (value == FindingStatus.AlreadyOptimal || value == FindingStatus.Applied)
                    IsSelected = false;
                OnPropertyChanged(); 
            }
        }
    }

    private bool _isSelected = true;

    /// <summary>Si el hallazgo está seleccionado en la interfaz para ser aplicado.</summary>
    public bool IsSelected 
    { 
        get => _isSelected; 
        set 
        { 
            if (_isSelected != value) 
            { 
                _isSelected = value; 
                OnPropertyChanged(); 
            } 
        } 
    }

    /// <summary>Si el cambio es reversible.</summary>
    public required bool IsReversible { get; init; }

    /// <summary>Si requiere privilegios de administrador.</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>Si requiere reinicio.</summary>
    public bool RequiresReboot { get; init; }

    /// <summary>Estimación orientativa de mejora.</summary>
    public required ImpactEstimate Impact { get; init; }

    /// <summary>Explicación para el usuario.</summary>
    public required FindingExplanation Explanation { get; init; }

    /// <summary>Fuentes de donde proviene la regla.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Presets en los que se incluye esta regla.</summary>
    public IReadOnlyList<Preset> Presets { get; init; } = [];

    /// <summary>Valor actual detectado en el sistema.</summary>
    public object? CurrentValue { get; set; }

    /// <summary>Valor recomendado.</summary>
    public object? RecommendedValue { get; init; }

    /// <summary>Fecha y hora de detección.</summary>
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
}
