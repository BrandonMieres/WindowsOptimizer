namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Nivel de confianza de una estimación de mejora.
/// </summary>
public enum Confidence
{
    /// <summary>Dato medido directamente en el equipo (ej: tamaño de archivos).</summary>
    High,

    /// <summary>Estimación basada en datos parciales (ej: contribución al arranque).</summary>
    Medium,

    /// <summary>Estimación aproximada, efecto discutido o no probado.</summary>
    Low
}
