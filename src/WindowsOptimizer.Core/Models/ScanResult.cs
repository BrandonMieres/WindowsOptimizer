using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Resultado completo de un análisis del sistema.
/// </summary>
public sealed class ScanResult
{
    /// <summary>Lista de hallazgos detectados.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>Puntuación de salud del sistema (0-100).</summary>
    public int HealthScore { get; init; }

    /// <summary>Fecha y hora del análisis.</summary>
    public DateTimeOffset ScannedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Duración del análisis.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Número de hallazgos por categoría.</summary>
    public IReadOnlyDictionary<Category, int> CountByCategory =>
        Findings
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Número de hallazgos por gravedad.</summary>
    public IReadOnlyDictionary<Severity, int> CountBySeverity =>
        Findings
            .GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Espacio total recuperable estimado (bytes).</summary>
    public long EstimatedReclaimableBytes =>
        Findings
            .Where(f => f.Impact.Type == ImpactType.DiskSpace && f.Impact.AbsoluteValue.HasValue)
            .Sum(f => f.Impact.AbsoluteValue!.Value);
}
