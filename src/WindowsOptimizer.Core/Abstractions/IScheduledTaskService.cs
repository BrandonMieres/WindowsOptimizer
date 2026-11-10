using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Abstracción para consultar y cambiar el estado de tareas programadas en Windows.
/// </summary>
public interface IScheduledTaskService
{
    /// <summary>
    /// Obtiene el estado actual de la tarea programada especificada por su ruta completa.
    /// Ejemplo: @"\Microsoft\Windows\Autochk\Proxy"
    /// </summary>
    Task<ScheduledTaskState> GetTaskStateAsync(string taskPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado de una tarea programada (Habilitar / Deshabilitar).
    /// </summary>
    Task<bool> SetTaskStateAsync(string taskPath, ScheduledTaskState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si la tarea existe en el sistema.
    /// </summary>
    Task<bool> TaskExistsAsync(string taskPath, CancellationToken cancellationToken = default);
}
