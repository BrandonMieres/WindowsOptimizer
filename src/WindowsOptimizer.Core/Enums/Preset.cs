namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Preset de optimización que agrupa reglas por nivel de agresividad.
/// </summary>
public enum Preset
{
    /// <summary>Solo cambios seguros para todos.</summary>
    Minimal,

    /// <summary>Recomendado para la mayoría de usuarios.</summary>
    Standard,

    /// <summary>Todos los cambios, incluidos los de riesgo alto.</summary>
    Advanced
}
