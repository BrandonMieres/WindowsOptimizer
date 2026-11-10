using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Estimación agregada del impacto conjunto de una selección de optimizaciones.
/// Calculada de forma conservadora a partir de métricas reales del sistema y reglas analizadas.
/// </summary>
public sealed record AggregateImpactEstimate
{
    /// <summary>Espacio total en disco recuperable en bytes (exacto).</summary>
    public long TotalRecoverableDiskBytes { get; init; }

    /// <summary>Porcentaje mínimo estimado de reducción en tiempo de arranque.</summary>
    public double EstimatedBootTimeReductionPercentageMin { get; init; }

    /// <summary>Porcentaje máximo estimado de reducción en tiempo de arranque.</summary>
    public double EstimatedBootTimeReductionPercentageMax { get; init; }

    /// <summary>Tiempo mínimo estimado recuperado al iniciar el sistema (si hay línea base de arranque).</summary>
    public TimeSpan? EstimatedBootTimeReductionMin { get; init; }

    /// <summary>Tiempo máximo estimado recuperado al iniciar el sistema (si hay línea base de arranque).</summary>
    public TimeSpan? EstimatedBootTimeReductionMax { get; init; }

    /// <summary>Memoria RAM física estimada liberada en bytes (servicios y procesos en segundo plano).</summary>
    public long EstimatedRamSavingsBytes { get; init; }

    /// <summary>Cantidad de ajustes orientados a privacidad y telemetría.</summary>
    public int PrivacyTweaksCount { get; init; }

    /// <summary>Cantidad de optimizaciones de rendimiento y gaming.</summary>
    public int PerformanceTweaksCount { get; init; }

    /// <summary>Cantidad de ajustes de sistema y personalización.</summary>
    public int SystemTweaksCount { get; init; }

    /// <summary>Número total de optimizaciones incluidas en el cálculo.</summary>
    public int TotalOptimizationsCount { get; init; }

    /// <summary>Nivel de confianza global de la estimación conjunta.</summary>
    public Confidence OverallConfidence { get; init; } = Confidence.High;

    /// <summary>Resumen textual legible para presentar al usuario en el Dashboard.</summary>
    public string SummaryDescription { get; init; } = string.Empty;
}
