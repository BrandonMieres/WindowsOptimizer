using System.Runtime.Versioning;
using System.Text.Json;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Acción correctiva que modifica valores del Registro de Windows.
/// Ejecuta los pasos del bloque apply[] de una regla y guarda snapshot previo.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryFixAction : IFixAction
{
    private readonly IRegistryAccess _registry;
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public RegistryFixAction(IRegistryAccess registry, RuleDefinition rule)
    {
        _registry = registry;
        _rule = rule;
    }

    public Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        // Usar el primer paso de apply[] que sea de tipo registry_set
        var step = _rule.Apply.FirstOrDefault(s =>
            s.Type.Equals("registry_set", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}' no tiene pasos de tipo registry_set.");

        var path = step.Path
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}': paso apply sin path.");
        var name = step.Name
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}': paso apply sin name.");

        // Capturar valor original antes de modificar
        var originalValue = _registry.GetValue(path, name);
        bool didNotExist = originalValue is null;

        // Resolver y aplicar el nuevo valor
        var newValue = step.Value
            ?? throw new InvalidOperationException($"La regla '{_rule.Id}': paso apply sin value.");
        var valueType = step.Kind ?? "DWord";

        object resolvedValue = ResolveValue(newValue, valueType);
        _registry.SetValue(path, name, resolvedValue, valueType);

        // Aplicar pasos adicionales (si la regla tiene múltiples apply[])
        foreach (var extraStep in _rule.Apply.Skip(1))
        {
            if (!extraStep.Type.Equals("registry_set", StringComparison.OrdinalIgnoreCase)) continue;
            if (extraStep.Path is null || extraStep.Name is null || extraStep.Value is null) continue;

            var extraValueType = extraStep.Kind ?? "DWord";
            object extraResolved = ResolveValue(extraStep.Value, extraValueType);
            _registry.SetValue(extraStep.Path, extraStep.Name, extraResolved, extraValueType);
        }

        var record = new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "registry_set",
            Target = $"{path}\\{name}",
            OriginalValue = originalValue?.ToString(),
            AppliedValue = resolvedValue.ToString(),
            OriginalDidNotExist = didNotExist,
            AppliedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(record);
    }

    public Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        var lastSeparator = record.Target.LastIndexOf('\\');
        if (lastSeparator < 0)
        {
            throw new InvalidOperationException($"Target '{record.Target}' no tiene formato válido.");
        }

        var path = record.Target[..lastSeparator];
        var name = record.Target[(lastSeparator + 1)..];

        if (record.OriginalDidNotExist)
        {
            _registry.DeleteValue(path, name);
        }
        else if (record.OriginalValue is not null)
        {
            var valueType = _rule.Apply.FirstOrDefault()?.Kind ?? "DWord";
            object resolvedValue = ResolveValue(record.OriginalValue, valueType);
            _registry.SetValue(path, name, resolvedValue, valueType);
        }

        return Task.CompletedTask;
    }

    private static object ResolveValue(object rawValue, string valueType)
    {
        string str;
        if (rawValue is JsonElement element)
        {
            str = element.ValueKind switch
            {
                JsonValueKind.Number => element.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonValueKind.String => element.GetString() ?? "",
                _ => element.GetRawText()
            };
        }
        else
        {
            str = rawValue.ToString() ?? "";
        }

        return valueType.ToLowerInvariant() switch
        {
            "dword" => int.TryParse(str, out var i) ? i : 0,
            "qword" => long.TryParse(str, out var l) ? l : 0L,
            "string" or "expandstring" => str,
            _ => str
        };
    }
}
