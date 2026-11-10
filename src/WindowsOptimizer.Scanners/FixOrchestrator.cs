using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Orquesta la aplicación y reversión de correcciones.
/// Coordina IFixAction + IRollbackStore para garantizar trazabilidad.
/// Soporta acciones de registro y de servicios.
/// </summary>
public sealed class FixOrchestrator
{
    private readonly IRollbackStore _rollbackStore;
    private readonly IRegistryAccess _registry;
    private readonly IServiceManager _serviceManager;
    private readonly IRuleProvider _ruleProvider;
    private readonly IAppxService _appxService;
    private readonly IPowerPlanService? _powerPlanService;
    private readonly IScheduledTaskService? _scheduledTaskService;

    public FixOrchestrator(
        IRollbackStore rollbackStore,
        IRegistryAccess registry,
        IServiceManager serviceManager,
        IRuleProvider ruleProvider,
        IAppxService appxService,
        IPowerPlanService? powerPlanService = null,
        IScheduledTaskService? scheduledTaskService = null)
    {
        _rollbackStore = rollbackStore;
        _registry = registry;
        _serviceManager = serviceManager;
        _ruleProvider = ruleProvider;
        _appxService = appxService;
        _powerPlanService = powerPlanService;
        _scheduledTaskService = scheduledTaskService;
    }

    /// <summary>
    /// Aplica la corrección para un hallazgo específico.
    /// </summary>
    public async Task<ChangeRecord> ApplyFixAsync(Finding finding, CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var rule = rules.FirstOrDefault(r => r.Id == finding.Id)
            ?? throw new InvalidOperationException($"No se encontró la regla '{finding.Id}'.");

        // Verificar permisos de Administrador si la regla lo requiere
        if (rule.RequiresAdmin && !Platform.ElevationHelper.IsAdmin())
        {
            throw new UnauthorizedAccessException($"La regla '{rule.Name}' requiere permisos de Administrador para ser aplicada.");
        }

        // Determinar el tipo de acción principal
        IFixAction fixAction = DetermineFixAction(rule);
        var changeRecord = await fixAction.ApplyAsync(cancellationToken);

        // Persistir en el almacén de rollback
        await _rollbackStore.SaveAsync(changeRecord, cancellationToken);

        // Actualizar el estado del hallazgo
        finding.Status = FindingStatus.Applied;

        return changeRecord;
    }

    /// <summary>
    /// Revierte un cambio previamente aplicado.
    /// </summary>
    public async Task RevertFixAsync(string changeRecordId, CancellationToken cancellationToken = default)
    {
        var record = await _rollbackStore.GetByIdAsync(changeRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró el cambio '{changeRecordId}'.");

        if (record.IsReverted)
        {
            throw new InvalidOperationException($"El cambio '{changeRecordId}' ya ha sido revertido.");
        }

        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var rule = rules.FirstOrDefault(r => r.Id == record.RuleId)
            ?? throw new InvalidOperationException($"No se encontró la regla '{record.RuleId}' para revertir.");

        if (rule.RequiresAdmin && !Platform.ElevationHelper.IsAdmin())
        {
            throw new UnauthorizedAccessException($"Revertir la regla '{rule.Name}' requiere permisos de Administrador.");
        }

        IFixAction fixAction = DetermineFixAction(rule);
        await fixAction.RevertAsync(record, cancellationToken);

        await _rollbackStore.MarkRevertedAsync(changeRecordId, cancellationToken);
    }

    /// <summary>
    /// Aplica todas las correcciones de un preset dado.
    /// </summary>
    public async Task<IReadOnlyList<ChangeRecord>> ApplyPresetAsync(
        Preset preset,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken = default)
    {
        var applicableFindings = findings
            .Where(f => (f.Status == FindingStatus.Detected || f.Status == FindingStatus.Drifted) && f.Presets.Contains(preset))
            .ToList();

        var results = new List<ChangeRecord>();
        foreach (var finding in applicableFindings)
        {
            try
            {
                var record = await ApplyFixAsync(finding, cancellationToken);
                results.Add(record);
            }
            catch (Exception)
            {
                // TODO: Log but continue
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Aplica una lista de correcciones basada en los IDs de los hallazgos.
    /// </summary>
    public async Task<IReadOnlyList<ChangeRecord>> ApplyFixesAsync(
        IEnumerable<string> findingIds,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ChangeRecord>();
        var idSet = new HashSet<string>(findingIds, StringComparer.OrdinalIgnoreCase);

        if (idSet.Count == 0) return results.AsReadOnly();

        // Creamos hallazgos dummy solo con el ID para reutilizar ApplyFixAsync
        // (idealmente ApplyFixAsync aceptaría string ruleId, pero por ahora esto sirve)
        var findings = idSet.Select(id => new Finding 
        { 
            Id = id, 
            Name = "", 
            Category = Category.Privacy, 
            Risk = RiskLevel.Low, 
            Severity = Severity.Low, 
            IsReversible = false,
            Impact = new ImpactEstimate { Type = ImpactType.Performance, Confidence = Confidence.Low, ShowPercentage = false },
            Explanation = new FindingExplanation { What = "", Why = "", Changes = "" }
        }).ToList();

        foreach (var finding in findings)
        {
            try
            {
                var record = await ApplyFixAsync(finding, cancellationToken);
                results.Add(record);
            }
            catch (Exception)
            {
                // TODO: Log but continue
            }
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Revierte todos los cambios activos.
    /// </summary>
    public async Task RevertAllAsync(CancellationToken cancellationToken = default)
    {
        var activeChanges = await _rollbackStore.GetActiveChangesAsync(cancellationToken);
        foreach (var change in activeChanges)
        {
            try
            {
                await RevertFixAsync(change.Id, cancellationToken);
            }
            catch (Exception)
            {
                // TODO: Log but continue
            }
        }
    }

    private IFixAction DetermineFixAction(RuleDefinition rule)
    {
        if (rule.Apply.Any(s => s.Type.Equals("appx_remove", StringComparison.OrdinalIgnoreCase)))
        {
            return new Platform.AppxFixAction(_appxService, rule);
        }

        if (rule.Apply.Any(s => s.Type.Equals("service_set", StringComparison.OrdinalIgnoreCase)))
        {
            return new Platform.ServiceFixAction(_serviceManager, rule);
        }

        if (rule.Apply.Any(s => s.Type.Equals("folder_clean", StringComparison.OrdinalIgnoreCase)))
        {
            return new Platform.CleanerFixAction(rule);
        }

        if (rule.Apply.Any(s => s.Type.Equals("command", StringComparison.OrdinalIgnoreCase)))
        {
            return new Platform.CommandFixAction(rule);
        }

        if (rule.Apply.Any(s => s.Type.Equals("power_plan_set", StringComparison.OrdinalIgnoreCase)))
        {
            if (_powerPlanService is null)
            {
                throw new InvalidOperationException("IPowerPlanService no está disponible para ejecutar la acción.");
            }
            return new Platform.PowerPlanFixAction(_powerPlanService, rule);
        }

        if (rule.Apply.Any(s => s.Type.Equals("scheduled_task_set", StringComparison.OrdinalIgnoreCase) ||
                                s.Type.Equals("scheduled_task", StringComparison.OrdinalIgnoreCase)))
        {
            if (_scheduledTaskService is null)
            {
                throw new InvalidOperationException("IScheduledTaskService no está disponible para ejecutar la acción.");
            }
            return new Platform.ScheduledTaskFixAction(_scheduledTaskService, rule);
        }

        // Por defecto: registry_set
        return new Platform.RegistryFixAction(_registry, rule);
    }
}
