using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Servicio para medir la línea base de rendimiento del sistema en reposo.
/// </summary>
public interface ISystemBaselineService
{
    /// <summary>
    /// Obtiene las métricas actuales del sistema (tiempo de arranque, RAM en uso, procesos y disco).
    /// </summary>
    Task<SystemBaseline> GetBaselineAsync(CancellationToken cancellationToken = default);
}
