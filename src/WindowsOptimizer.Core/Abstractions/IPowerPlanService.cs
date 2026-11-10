using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Servicio para consultar, activar y gestionar planes de energía de Windows.
/// </summary>
public interface IPowerPlanService
{
    /// <summary>
    /// Obtiene todos los planes de energía disponibles en el sistema.
    /// </summary>
    Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el plan de energía actualmente activo en el sistema.
    /// </summary>
    Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa el plan de energía especificado por su GUID.
    /// </summary>
    Task<bool> SetActivePlanAsync(string planGuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Habilita o activa el plan 'Máximo Rendimiento' (Ultimate Performance).
    /// Si ya existe uno creado, lo reutiliza y activa para evitar duplicados.
    /// </summary>
    Task<PowerPlanInfo?> EnableUltimatePerformanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un plan de energía personalizado mediante su GUID (no permite eliminar esquemas base del sistema).
    /// </summary>
    Task<bool> DeletePlanAsync(string planGuid, CancellationToken cancellationToken = default);
}
