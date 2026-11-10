using System.Runtime.Versioning;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Acción correctiva que modifica el tipo de inicio de un servicio de Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ServiceFixAction : IFixAction
{
    private readonly IServiceManager _serviceManager;
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public ServiceFixAction(IServiceManager serviceManager, RuleDefinition rule)
    {
        _serviceManager = serviceManager;
        _rule = rule;
    }

    public Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var step = _rule.Apply.FirstOrDefault(s =>
            s.Type.Equals("service_set", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}' no tiene pasos de tipo service_set.");

        var serviceName = step.ServiceName
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}': paso apply sin service_name.");

        // Capturar estado actual antes de modificar
        var currentInfo = _serviceManager.GetServiceInfo(serviceName);
        string originalStartType = currentInfo?.StartType.ToString() ?? "Unknown";
        string originalStatus = currentInfo?.Status.ToString() ?? "Unknown";

        // Aplicar nuevo tipo de inicio
        var newStartType = step.StartType ?? "Disabled";
        if (Enum.TryParse<ServiceStartMode>(newStartType, true, out var mode))
        {
            _serviceManager.SetStartupType(serviceName, mode);
        }

        // Detener el servicio si se indica
        if (step.Stop)
        {
            _serviceManager.StopService(serviceName);
        }

        var record = new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "service_set",
            Target = serviceName,
            OriginalValue = $"{originalStartType}|{originalStatus}",
            AppliedValue = $"{newStartType}|{(step.Stop ? "Stopped" : originalStatus)}",
            OriginalDidNotExist = currentInfo is null,
            AppliedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(record);
    }

    public Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        var serviceName = record.Target;
        var parts = record.OriginalValue?.Split('|') ?? [];

        if (parts.Length >= 1 && Enum.TryParse<ServiceStartMode>(parts[0], true, out var originalMode))
        {
            _serviceManager.SetStartupType(serviceName, originalMode);
        }

        // Si el servicio estaba corriendo antes, reiniciarlo
        if (parts.Length >= 2 && parts[1].Equals("Running", StringComparison.OrdinalIgnoreCase))
        {
            _serviceManager.StartService(serviceName);
        }

        return Task.CompletedTask;
    }
}
