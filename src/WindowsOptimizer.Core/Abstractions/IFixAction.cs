using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Interfaz para una acción correctiva que se puede aplicar y revertir.
/// </summary>
public interface IFixAction
{
    /// <summary>Id de la regla asociada.</summary>
    string RuleId { get; }

    /// <summary>Si la acción requiere privilegios de administrador.</summary>
    bool RequiresAdmin { get; }

    /// <summary>
    /// Aplica el cambio y devuelve el registro para rollback.
    /// </summary>
    Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revierte un cambio previamente aplicado.
    /// </summary>
    Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default);
}
