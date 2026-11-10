namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Gravedad del hallazgo detectado durante el análisis.
/// </summary>
public enum Severity
{
    /// <summary>Solo informativo, sin acción necesaria.</summary>
    Info,

    /// <summary>Mejora menor recomendada.</summary>
    Low,

    /// <summary>Mejora significativa recomendada.</summary>
    Medium,

    /// <summary>Problema importante que debería corregirse.</summary>
    High,

    /// <summary>Problema crítico que requiere atención inmediata.</summary>
    Critical
}
