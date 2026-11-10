namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Estado de un hallazgo respecto a su aplicación.
/// </summary>
public enum FindingStatus
{
    /// <summary>Se detectó que la regla aplica (hay algo que mejorar).</summary>
    Detected,

    /// <summary>La regla ya está en el estado óptimo.</summary>
    AlreadyOptimal,

    /// <summary>El cambio fue aplicado por el usuario.</summary>
    Applied,

    /// <summary>El cambio fue revertido al estado original.</summary>
    Reverted,

    /// <summary>Windows u otra fuente revertió el cambio (drift).</summary>
    Drifted,

    /// <summary>No se pudo determinar el estado.</summary>
    Unknown
}
