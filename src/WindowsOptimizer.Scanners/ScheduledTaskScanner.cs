using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Analizador que evalúa reglas basadas en tareas programadas de Windows (Task Scheduler).
/// Detecta tareas de telemetría y diagnóstico activas y propone su desactivación reversible.
/// </summary>
public sealed class ScheduledTaskScanner : IScanner
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IScheduledTaskService _scheduledTaskService;

    public string Name => "Scheduled Task Scanner";
    public Category Category { get; }

    public ScheduledTaskScanner(
        IRuleProvider ruleProvider,
        IScheduledTaskService scheduledTaskService,
        Category category = Category.Privacy)
    {
        _ruleProvider = ruleProvider;
        _scheduledTaskService = scheduledTaskService;
        Category = category;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var taskRules = rules
            .Where(r => r.Detect?.ScheduledTask is not null && r.Category == Category)
            .ToList();

        var findings = new List<Finding>();

        foreach (var rule in taskRules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detect = rule.Detect!.ScheduledTask!;
            var currentState = await _scheduledTaskService.GetTaskStateAsync(detect.Path, cancellationToken);

            // Si la tarea no existe en este sistema o edición de Windows, no se reporta hallazgo
            if (currentState == ScheduledTaskState.NotFound)
            {
                continue;
            }

            bool isOptimal = false;
            if (!string.IsNullOrWhiteSpace(detect.ExpectedState) &&
                Enum.TryParse<ScheduledTaskState>(detect.ExpectedState, true, out var expected))
            {
                isOptimal = (currentState == expected);
            }
            else if (!string.IsNullOrWhiteSpace(detect.BadState) &&
                     Enum.TryParse<ScheduledTaskState>(detect.BadState, true, out var bad))
            {
                isOptimal = (currentState != bad);
            }
            else
            {
                // Por defecto: óptimo es Disabled
                isOptimal = (currentState == ScheduledTaskState.Disabled);
            }

            var severity = rule.Risk switch
            {
                RiskLevel.Low    => Severity.Medium,
                RiskLevel.Medium => Severity.High,
                RiskLevel.High   => Severity.Critical,
                _                => Severity.Low
            };

            var explanation = rule.Explain != null
                ? new FindingExplanation
                {
                    What = rule.Explain.What,
                    Why = rule.Explain.Why,
                    Changes = rule.Explain.Changes
                }
                : new FindingExplanation
                {
                    What = rule.Description ?? rule.Name,
                    Why = "Innecesaria para el uso diario; consume ciclos de CPU y envía telemetría de fondo.",
                    Changes = "Deshabilita la tarea en el Programador de Tareas."
                };

            var impact = rule.Impact != null
                ? new ImpactEstimate
                {
                    Type = rule.Impact.Type,
                    Confidence = rule.Impact.Confidence,
                    ShowPercentage = rule.Impact.ShowPercentage
                }
                : new ImpactEstimate
                {
                    Type = ImpactType.Privacy,
                    Confidence = Confidence.High,
                    ShowPercentage = false
                };

            findings.Add(new Finding
            {
                Id               = rule.Id,
                Name             = rule.Name,
                Description      = rule.Description,
                Category         = rule.Category,
                Severity         = severity,
                Risk             = rule.Risk,
                IsReversible     = rule.Reversible,
                RequiresAdmin    = rule.RequiresAdmin,
                RequiresReboot   = rule.RequiresReboot,
                Presets          = rule.Presets,
                Impact           = impact,
                Explanation      = explanation,
                Sources          = rule.Sources,
                CurrentValue     = currentState.ToString(),
                RecommendedValue = detect.ExpectedState ?? ScheduledTaskState.Disabled.ToString(),
                Status           = isOptimal ? FindingStatus.AlreadyOptimal : FindingStatus.Detected
            });
        }

        return findings.AsReadOnly();
    }
}
