using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Acción correctiva para cambiar el estado de una tarea programada (habilitar/deshabilitar)
/// y permitir su posterior reversión al estado original.
/// </summary>
public sealed class ScheduledTaskFixAction : IFixAction
{
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public ScheduledTaskFixAction(IScheduledTaskService scheduledTaskService, RuleDefinition rule)
    {
        _scheduledTaskService = scheduledTaskService;
        _rule = rule;
    }

    public async Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var step = _rule.Apply.FirstOrDefault(s =>
            s.Type.Equals("scheduled_task", StringComparison.OrdinalIgnoreCase) ||
            s.Type.Equals("scheduled_task_set", StringComparison.OrdinalIgnoreCase));

        string taskPath = step?.Path ?? _rule.Detect?.ScheduledTask?.Path
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}' no especifica una ruta de tarea programada.");

        var currentState = await _scheduledTaskService.GetTaskStateAsync(taskPath, cancellationToken);

        ScheduledTaskState targetState = ScheduledTaskState.Disabled;
        if (!string.IsNullOrWhiteSpace(step?.State) &&
            Enum.TryParse<ScheduledTaskState>(step.State, true, out var parsedState))
        {
            targetState = parsedState;
        }

        bool success = await _scheduledTaskService.SetTaskStateAsync(taskPath, targetState, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"No se pudo cambiar el estado de la tarea programada '{taskPath}' a {targetState}.");
        }

        return new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "scheduled_task_set",
            Target = taskPath,
            OriginalValue = currentState.ToString(),
            AppliedValue = targetState.ToString(),
            AppliedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        string taskPath = record.Target;
        if (string.IsNullOrWhiteSpace(taskPath))
        {
            var step = _rule.Apply.FirstOrDefault(s =>
                s.Type.Equals("scheduled_task", StringComparison.OrdinalIgnoreCase) ||
                s.Type.Equals("scheduled_task_set", StringComparison.OrdinalIgnoreCase));
            taskPath = step?.Path ?? _rule.Detect?.ScheduledTask?.Path ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(taskPath))
            return;

        ScheduledTaskState revertState = ScheduledTaskState.Enabled;
        if (!string.IsNullOrWhiteSpace(record.OriginalValue) &&
            Enum.TryParse<ScheduledTaskState>(record.OriginalValue, true, out var parsedOriginal))
        {
            revertState = parsedOriginal;
        }

        await _scheduledTaskService.SetTaskStateAsync(taskPath, revertState, cancellationToken);
    }
}
