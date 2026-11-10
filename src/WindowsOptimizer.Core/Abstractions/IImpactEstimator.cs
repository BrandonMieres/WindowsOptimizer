using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Motor de estimación orientativa del impacto antes de aplicar un conjunto de optimizaciones.
/// </summary>
public interface IImpactEstimator
{
    /// <summary>
    /// Calcula la estimación agregada de espacio, tiempo de arranque, RAM y ajustes de privacidad.
    /// </summary>
    /// <param name="findings">Lista de hallazgos que se van a aplicar.</param>
    /// <param name="baseline">Línea base opcional del sistema para convertir porcentajes a tiempos reales.</param>
    AggregateImpactEstimate EstimateImpact(IEnumerable<Finding> findings, SystemBaseline? baseline = null);
}
