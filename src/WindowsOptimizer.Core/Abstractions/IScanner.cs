using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Interfaz para un módulo de análisis del sistema (solo lectura).
/// Cada scanner revisa un aspecto concreto y produce hallazgos.
/// </summary>
public interface IScanner
{
    /// <summary>Nombre del scanner.</summary>
    string Name { get; }

    /// <summary>Categoría que analiza.</summary>
    Enums.Category Category { get; }

    /// <summary>
    /// Ejecuta el análisis y devuelve los hallazgos detectados.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lista de hallazgos.</returns>
    Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default);
}
