namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Proporciona la capacidad de crear puntos de restauración del sistema operativo.
/// </summary>
public interface ISystemRestoreService
{
    /// <summary>
    /// Crea un punto de restauración del sistema de manera asíncrona.
    /// </summary>
    /// <param name="description">El nombre descriptivo del punto de restauración.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>True si el punto de restauración se creó correctamente; de lo contrario, False.</returns>
    Task<bool> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}
