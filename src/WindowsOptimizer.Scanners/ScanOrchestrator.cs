using System.Diagnostics;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Orquesta la ejecución de todos los scanners registrados.
/// </summary>
public sealed class ScanOrchestrator
{
    private readonly IEnumerable<IScanner> _scanners;
    private readonly IRollbackStore _rollbackStore;

    public ScanOrchestrator(IEnumerable<IScanner> scanners, IRollbackStore rollbackStore)
    {
        _scanners = scanners;
        _rollbackStore = rollbackStore;
    }

    /// <summary>
    /// Ejecuta todos los analizadores y consolida los resultados.
    /// Detecta 'Drift' (desviación) comparando con el historial de reversiones.
    /// </summary>
    public async Task<ScanResult> RunFullScanAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var allFindings = new List<Finding>();

        // 1. Obtener historial de cambios activos (lo que nuestra app ha aplicado)
        var activeChanges = await _rollbackStore.GetActiveChangesAsync(cancellationToken);
        var appliedRuleIds = new HashSet<string>(activeChanges.Select(c => c.RuleId), StringComparer.OrdinalIgnoreCase);

        var tasks = _scanners.Select(s => s.ScanAsync(cancellationToken));
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            foreach (var finding in result)
            {
                if (appliedRuleIds.Contains(finding.Id))
                {
                    if (finding.Status == Core.Enums.FindingStatus.AlreadyOptimal)
                    {
                        // Si está óptimo y lo aplicamos nosotros, su estado es Applied
                        finding.Status = Core.Enums.FindingStatus.Applied;
                    }
                    else if (finding.Status == Core.Enums.FindingStatus.Detected)
                    {
                        // Si vuelve a estar detectado (malo) pero lo habíamos aplicado, es Drift (Windows lo deshizo)
                        finding.Status = Core.Enums.FindingStatus.Drifted;
                    }
                }
                
                allFindings.Add(finding);
            }
        }

        sw.Stop();

        int healthScore = CalculateHealthScore(allFindings);

        return new ScanResult
        {
            Findings = allFindings.AsReadOnly(),
            Duration = sw.Elapsed,
            ScannedAt = DateTimeOffset.UtcNow,
            HealthScore = healthScore
        };
    }

    private static int CalculateHealthScore(List<Finding> findings)
    {
        if (findings.Count == 0) return 100;

        // Base: 100 puntos. Restamos puntos según los hallazgos que no sean AlreadyOptimal
        int score = 100;
        var pendingFindings = findings.Where(f => f.Status == Core.Enums.FindingStatus.Detected || f.Status == Core.Enums.FindingStatus.Drifted);

        foreach (var finding in pendingFindings)
        {
            score -= finding.Severity switch
            {
                Core.Enums.Severity.Critical => 15,
                Core.Enums.Severity.High => 8,
                Core.Enums.Severity.Medium => 4,
                Core.Enums.Severity.Low => 2,
                Core.Enums.Severity.Info => 0,
                _ => 1
            };
        }

        return Math.Max(0, Math.Min(100, score));
    }
}
