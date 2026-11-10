namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Tipo de impacto esperado de una regla.
/// Determina cómo se estima y presenta la mejora al usuario.
/// </summary>
public enum ImpactType
{
    /// <summary>Mejora en el tiempo de arranque.</summary>
    BootTime,

    /// <summary>Mejora en uso de memoria RAM.</summary>
    Ram,

    /// <summary>Espacio en disco recuperable.</summary>
    DiskSpace,

    /// <summary>Mejora en latencia de red.</summary>
    Latency,

    /// <summary>Reducción de datos enviados (privacidad).</summary>
    Privacy,

    /// <summary>Sin impacto medible o impacto no estimable.</summary>
    None,

    /// <summary>Rendimiento general (CPU/IO).</summary>
    Performance,

    /// <summary>Seguridad del sistema.</summary>
    Security,

    /// <summary>Productividad y usabilidad de interfaz.</summary>
    Productivity,

    /// <summary>Almacenamiento y gestión de archivos.</summary>
    Storage
}
