using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Almacén de registros de cambios para rollback.
/// </summary>
public interface IRollbackStore
{
    /// <summary>Guarda un registro de cambio.</summary>
    Task SaveAsync(ChangeRecord record, CancellationToken cancellationToken = default);

    /// <summary>Obtiene todos los cambios activos (no revertidos).</summary>
    Task<IReadOnlyList<ChangeRecord>> GetActiveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene todo el historial de cambios.</summary>
    Task<IReadOnlyList<ChangeRecord>> GetHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene un cambio por su id.</summary>
    Task<ChangeRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Marca un cambio como revertido.</summary>
    Task MarkRevertedAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Limpia todo el historial de cambios.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
