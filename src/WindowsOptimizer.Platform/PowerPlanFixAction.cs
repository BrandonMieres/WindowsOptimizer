using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Acción correctiva para cambiar el plan de energía activo y permitir su posterior reversión.
/// </summary>
public sealed class PowerPlanFixAction : IFixAction
{
    private readonly IPowerPlanService _powerPlanService;
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public PowerPlanFixAction(IPowerPlanService powerPlanService, RuleDefinition rule)
    {
        _powerPlanService = powerPlanService;
        _rule = rule;
    }

    public async Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var currentPlan = await _powerPlanService.GetActivePlanAsync(cancellationToken);
        string originalGuid = currentPlan?.Guid ?? PowerPlanService.BalancedGuid;

        // Buscar si hay un plan objetivo específico en la regla
        var step = _rule.Apply.FirstOrDefault(s => s.Type.Equals("power_plan_set", StringComparison.OrdinalIgnoreCase));
        string targetGuid = PowerPlanService.HighPerformanceGuid;

        if (step?.TargetScheme?.Equals("UltimatePerformance", StringComparison.OrdinalIgnoreCase) == true)
        {
            var ultimate = await _powerPlanService.EnableUltimatePerformanceAsync(cancellationToken);
            targetGuid = ultimate?.Guid ?? PowerPlanService.HighPerformanceGuid;
        }
        else if (!string.IsNullOrWhiteSpace(step?.TargetScheme) && Guid.TryParse(step.TargetScheme, out _))
        {
            targetGuid = step.TargetScheme;
            await _powerPlanService.SetActivePlanAsync(targetGuid, cancellationToken);
        }
        else
        {
            // Por defecto: High Performance si no se especifica otro
            await _powerPlanService.SetActivePlanAsync(targetGuid, cancellationToken);
        }

        return new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "power_plan_set",
            Target = "PowerScheme",
            OriginalValue = originalGuid,
            AppliedValue = targetGuid,
            AppliedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        string revertGuid = !string.IsNullOrWhiteSpace(record.OriginalValue)
            ? record.OriginalValue
            : PowerPlanService.BalancedGuid;

        await _powerPlanService.SetActivePlanAsync(revertGuid, cancellationToken);
    }
}
