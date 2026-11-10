using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Measurement;

/// <summary>
/// Motor de estimación orientativa del impacto antes de aplicar un lote de optimizaciones.
/// Sigue las directrices de PROYECTO.md §8.2: prudente, basado en datos reales y sin falsas promesas.
/// </summary>
public sealed class ImpactEstimator : IImpactEstimator
{
    public AggregateImpactEstimate EstimateImpact(IEnumerable<Finding> findings, SystemBaseline? baseline = null)
    {
        var list = findings
            .Where(f => f.Status is FindingStatus.Detected or FindingStatus.Drifted)
            .ToList();

        if (list.Count == 0)
        {
            return new AggregateImpactEstimate
            {
                TotalRecoverableDiskBytes = 0,
                EstimatedBootTimeReductionPercentageMin = 0,
                EstimatedBootTimeReductionPercentageMax = 0,
                EstimatedRamSavingsBytes = 0,
                PrivacyTweaksCount = 0,
                PerformanceTweaksCount = 0,
                SystemTweaksCount = 0,
                TotalOptimizationsCount = 0,
                OverallConfidence = Confidence.High,
                SummaryDescription = "No hay optimizaciones pendientes seleccionadas."
            };
        }

        // 1. Espacio en disco recuperable
        long totalDiskBytes = 0;
        foreach (var f in list.Where(f => f.Impact.Type is ImpactType.DiskSpace or ImpactType.Storage || f.Category == Category.Cleaners))
        {
            if (f.Impact.AbsoluteValue.HasValue && f.Impact.AbsoluteValue.Value > 0)
            {
                totalDiskBytes += f.Impact.AbsoluteValue.Value;
            }
        }

        // 2. Tiempo de arranque (Startup apps y servicios de inicio)
        var startupFindings = list.Where(f => f.Category == Category.Startup || f.Impact.Type == ImpactType.BootTime).ToList();
        double minBootPct = 0;
        double maxBootPct = 0;
        TimeSpan? minBootTime = null;
        TimeSpan? maxBootTime = null;

        if (startupFindings.Count > 0)
        {
            // Cada app de inicio eliminada suele aportar entre un 2.0% y un 4.5% de aceleración
            minBootPct = Math.Min(35.0, startupFindings.Count * 2.0);
            maxBootPct = Math.Min(45.0, startupFindings.Count * 4.5);

            if (baseline?.BootDuration.HasValue == true && baseline.BootDuration.Value.TotalSeconds > 3)
            {
                double totalMs = baseline.BootDuration.Value.TotalMilliseconds;
                minBootTime = TimeSpan.FromMilliseconds(totalMs * (minBootPct / 100.0));
                maxBootTime = TimeSpan.FromMilliseconds(totalMs * (maxBootPct / 100.0));
            }
        }

        // 3. Ahorro de memoria RAM estimado (servicios y procesos)
        long estimatedRamBytes = 0;
        foreach (var f in list.Where(f => f.Impact.Type == ImpactType.Ram || f.Category == Category.Services))
        {
            if (f.Impact.AbsoluteValue.HasValue && f.Impact.AbsoluteValue.Value > 0)
            {
                estimatedRamBytes += f.Impact.AbsoluteValue.Value;
            }
            else
            {
                // Promedio conservador de ~25 MB por servicio en segundo plano desactivado
                estimatedRamBytes += 25L * 1024 * 1024;
            }
        }

        // 4. Conteo por categorías funcionales
        int privacyCount = list.Count(f => f.Category == Category.Privacy || f.Impact.Type == ImpactType.Privacy);
        int performanceCount = list.Count(f => f.Category is Category.Performance or Category.Gaming or Category.Network || f.Impact.Type == ImpactType.Performance);
        int systemCount = list.Count(f => f.Category is Category.System or Category.Ui || f.Impact.Type is ImpactType.Productivity or ImpactType.Security);

        // 5. Confianza global
        var overallConfidence = Confidence.High;
        if (list.Any(f => f.Impact.Confidence == Confidence.Low))
        {
            overallConfidence = Confidence.Medium;
        }

        // 6. Resumen descriptivo
        string summary = BuildSummaryDescription(totalDiskBytes, minBootPct, maxBootPct, minBootTime, maxBootTime, estimatedRamBytes, privacyCount, performanceCount);

        return new AggregateImpactEstimate
        {
            TotalRecoverableDiskBytes = totalDiskBytes,
            EstimatedBootTimeReductionPercentageMin = minBootPct,
            EstimatedBootTimeReductionPercentageMax = maxBootPct,
            EstimatedBootTimeReductionMin = minBootTime,
            EstimatedBootTimeReductionMax = maxBootTime,
            EstimatedRamSavingsBytes = estimatedRamBytes,
            PrivacyTweaksCount = privacyCount,
            PerformanceTweaksCount = performanceCount,
            SystemTweaksCount = systemCount,
            TotalOptimizationsCount = list.Count,
            OverallConfidence = overallConfidence,
            SummaryDescription = summary
        };
    }

    private static string BuildSummaryDescription(
        long diskBytes,
        double minBootPct,
        double maxBootPct,
        TimeSpan? minBootTime,
        TimeSpan? maxBootTime,
        long ramBytes,
        int privacyCount,
        int perfCount)
    {
        var parts = new List<string>();

        if (diskBytes > 0)
        {
            double mb = diskBytes / (1024.0 * 1024.0);
            if (mb >= 1024.0)
            {
                parts.Add($"~{(mb / 1024.0):F1} GB de espacio");
            }
            else
            {
                parts.Add($"~{mb:F0} MB de espacio");
            }
        }

        if (maxBootPct > 0)
        {
            if (minBootTime.HasValue && maxBootTime.HasValue)
            {
                parts.Add($"arranque ~{minBootTime.Value.TotalSeconds:F1}-{maxBootTime.Value.TotalSeconds:F1}s más rápido");
            }
            else
            {
                parts.Add($"arranque ~{minBootPct:F0}-{maxBootPct:F0}% más rápido");
            }
        }

        if (ramBytes > 0 && perfCount > 0)
        {
            double ramMb = ramBytes / (1024.0 * 1024.0);
            parts.Add($"~{ramMb:F0} MB de RAM liberable");
        }

        if (privacyCount > 0)
        {
            parts.Add($"{privacyCount} ajustes de privacidad");
        }

        if (parts.Count == 0)
        {
            return "Optimización de configuración del sistema.";
        }

        return "Mejora estimada: " + string.Join(", ", parts) + ".";
    }
}
