using System.Runtime.Versioning;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Analiza el plan de energía activo de Windows y genera hallazgos si el sistema
/// está configurado en un modo que degrada el rendimiento para Gaming o Performance.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PowerPlanScanner : IScanner
{
    private readonly IPowerPlanService _powerPlanService;
    private readonly IRuleProvider _ruleProvider;

    public string Name => "Power Plan Scanner";
    public Category Category { get; }

    public PowerPlanScanner(
        IPowerPlanService powerPlanService,
        IRuleProvider ruleProvider,
        Category category = Category.Gaming)
    {
        _powerPlanService = powerPlanService;
        _ruleProvider = ruleProvider;
        Category = category;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();
        var activePlan = await _powerPlanService.GetActivePlanAsync(cancellationToken);

        var allRules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var powerRules = allRules
            .Where(r => r.Category == Category && r.Detect?.PowerPlan is not null)
            .ToList();

        if (powerRules.Count > 0)
        {
            foreach (var rule in powerRules)
            {
                var detect = rule.Detect!.PowerPlan!;
                bool isOptimal = false;

                if (activePlan != null && detect.ExpectedSchemes != null)
                {
                    isOptimal = detect.ExpectedSchemes.Any(expected =>
                        expected.Equals(activePlan.SchemeType.ToString(), StringComparison.OrdinalIgnoreCase) ||
                        expected.Equals(activePlan.Guid, StringComparison.OrdinalIgnoreCase) ||
                        activePlan.Name.Contains(expected, StringComparison.OrdinalIgnoreCase));
                }

                var severity = rule.Risk switch
                {
                    RiskLevel.Low => Severity.Low,
                    RiskLevel.Medium => Severity.Medium,
                    RiskLevel.High => Severity.High,
                    _ => Severity.Info
                };

                findings.Add(new Finding
                {
                    Id = rule.Id,
                    Name = rule.Name,
                    Description = rule.Description ?? $"Plan activo actual: {activePlan?.Name ?? "Desconocido"}.",
                    Category = rule.Category,
                    Severity = severity,
                    Risk = rule.Risk,
                    Status = isOptimal ? FindingStatus.AlreadyOptimal : FindingStatus.Detected,
                    IsReversible = rule.Reversible,
                    Presets = rule.Presets ?? new List<Preset> { Preset.Standard, Preset.Advanced },
                    Impact = rule.Impact is not null
                        ? new ImpactEstimate
                        {
                            Type = rule.Impact.Type,
                            Confidence = rule.Impact.Confidence,
                            ShowPercentage = rule.Impact.ShowPercentage
                        }
                        : new ImpactEstimate
                        {
                            Type = ImpactType.Performance,
                            Confidence = Confidence.High,
                            ShowPercentage = false
                        },
                    Explanation = new FindingExplanation
                    {
                        What = rule.Explain?.What ?? "Control del plan de energía del sistema.",
                        Why = rule.Explain?.Why ?? "Un plan de alto rendimiento evita caídas de frecuencia de reloj y core parking.",
                        Changes = rule.Explain?.Changes ?? "Establece el plan activo en Alto Rendimiento o Máximo Rendimiento."
                    }
                });
            }
        }
        else
        {
            // Si la categoría no tiene regla específica en el JSON, creamos una regla sintética para la categoría
            bool isHighPerf = activePlan != null && (
                activePlan.SchemeType == PowerSchemeType.HighPerformance ||
                activePlan.SchemeType == PowerSchemeType.UltimatePerformance);

            findings.Add(new Finding
            {
                Id = $"{Category.ToString().ToLowerInvariant()}.powerplan-performance",
                Name = "Plan de energía de Máximo Rendimiento",
                Description = activePlan != null
                    ? $"Plan actual: '{activePlan.Name}'. Se recomienda un plan de alto rendimiento para exprimir tu hardware sin micro-parones."
                    : "No se detectó un plan de energía activo de alto rendimiento.",
                Category = Category,
                Severity = Severity.Medium,
                Risk = RiskLevel.Low,
                Status = isHighPerf ? FindingStatus.AlreadyOptimal : FindingStatus.Detected,
                IsReversible = true,
                Presets = new List<Preset> { Preset.Standard, Preset.Advanced },
                Impact = new ImpactEstimate
                {
                    Type = ImpactType.Performance,
                    Confidence = Confidence.High,
                    ShowPercentage = false
                },
                Explanation = new FindingExplanation
                {
                    What = "Windows suele configurar por defecto el plan 'Equilibrado', que apaga núcleos y retrasa el aumento de frecuencia ante picos de demanda.",
                    Why = "Activar Alto Rendimiento o Máximo Rendimiento estabiliza los fotogramas por segundo (frametimes) y elimina micro-tirones (stuttering).",
                    Changes = "Activa el plan de Alto Rendimiento o duplica el plan oculto de Máximo Rendimiento."
                }
            });
        }

        return findings.AsReadOnly();
    }
}
