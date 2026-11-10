namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Registro de un cambio aplicado, almacenado para permitir rollback.
/// </summary>
public sealed class ChangeRecord
{
    /// <summary>Identificador único del cambio.</summary>
    public required string Id { get; init; }

    /// <summary>Id de la regla que originó el cambio.</summary>
    public required string RuleId { get; init; }

    /// <summary>Nombre legible del cambio.</summary>
    public required string Name { get; init; }

    /// <summary>Tipo de cambio (registry_set, service_set, etc.).</summary>
    public required string ActionType { get; init; }

    /// <summary>Ruta o recurso afectado (clave de registro, nombre de servicio...).</summary>
    public required string Target { get; init; }

    /// <summary>Valor original antes del cambio (serializado como string).</summary>
    public string? OriginalValue { get; init; }

    /// <summary>Valor aplicado (serializado como string).</summary>
    public string? AppliedValue { get; init; }

    /// <summary>Si la clave/recurso no existía antes del cambio.</summary>
    public bool OriginalDidNotExist { get; init; }

    /// <summary>Fecha y hora en que se aplicó el cambio.</summary>
    public DateTimeOffset AppliedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Fecha y hora en que se revirtió (null si no se ha revertido).</summary>
    public DateTimeOffset? RevertedAt { get; set; }

    /// <summary>Si el cambio ha sido revertido.</summary>
    public bool IsReverted => RevertedAt.HasValue;
}
