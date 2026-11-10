using System.Text.Json.Serialization;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Definición cruda de una regla cargada desde el JSON.
/// El esquema refleja exactamente el formato de privacy.json y otros archivos de reglas.
/// </summary>
public sealed class RuleDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("category")]
    public required Category Category { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("risk")]
    public required RiskLevel Risk { get; init; }

    [JsonPropertyName("presets")]
    public List<Preset> Presets { get; init; } = [];

    [JsonPropertyName("detect")]
    public DetectBlock? Detect { get; init; }

    [JsonPropertyName("apply")]
    public List<ApplyStep> Apply { get; init; } = [];

    [JsonPropertyName("reversible")]
    public bool Reversible { get; init; } = true;

    [JsonPropertyName("requires_admin")]
    public bool RequiresAdmin { get; init; } = true;

    [JsonPropertyName("requires_reboot")]
    public bool RequiresReboot { get; init; }

    [JsonPropertyName("explain")]
    public ExplainBlock? Explain { get; init; }

    [JsonPropertyName("impact")]
    public ImpactBlock? Impact { get; init; }

    [JsonPropertyName("sources")]
    public List<string> Sources { get; init; } = [];

    /// <summary>
    /// Si es true, la regla solo aplica cuando el disco del sistema es SSD/NVMe.
    /// En HDD se omite el hallazgo para no recomendar desactivar SysMain/WSearch.
    /// </summary>
    [JsonPropertyName("ssd_only")]
    public bool SsdOnly { get; init; }
}

public sealed class DetectBlock
{
    [JsonPropertyName("registry")]
    public RegistryDetect? Registry { get; init; }

    [JsonPropertyName("service")]
    public ServiceDetect? Service { get; init; }

    [JsonPropertyName("folder")]
    public FolderDetect? Folder { get; init; }

    [JsonPropertyName("appx")]
    public AppxDetect? Appx { get; init; }

    [JsonPropertyName("power_plan")]
    public PowerPlanDetect? PowerPlan { get; init; }

    [JsonPropertyName("scheduled_task")]
    public ScheduledTaskDetect? ScheduledTask { get; init; }
}

public sealed class ScheduledTaskDetect
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("expected_state")]
    public string? ExpectedState { get; init; }

    [JsonPropertyName("bad_state")]
    public string? BadState { get; init; }
}

public sealed class PowerPlanDetect
{
    [JsonPropertyName("expected_schemes")]
    public List<string>? ExpectedSchemes { get; init; }
}

public sealed class RegistryDetect
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Valor esperado = óptimo (si coincide, ya está bien).</summary>
    [JsonPropertyName("expected_value")]
    public object? ExpectedValue { get; init; }

    /// <summary>Valor "malo" (si coincide, hay que corregir).</summary>
    [JsonPropertyName("bad_value")]
    public object? BadValue { get; init; }
}

public sealed class ApplyStep
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    // Registry fields
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }

    // Service fields
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    [JsonPropertyName("start_type")]
    public string? StartType { get; init; }

    [JsonPropertyName("stop")]
    public bool Stop { get; init; }

    // AppX fields
    [JsonPropertyName("package_name")]
    public string? PackageName { get; init; }

    [JsonPropertyName("undo")]
    public object? Undo { get; init; }

    [JsonPropertyName("target_scheme")]
    public string? TargetScheme { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }
}

public sealed class AppxDetect
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed class ServiceDetect
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("bad_start_type")]
    public string? BadStartType { get; init; }

    [JsonPropertyName("expected_start_type")]
    public string? ExpectedStartType { get; init; }

    /// <summary>
    /// Si está definido, el servicio solo se evalúa cuando el disco del sistema
    /// cumple este requisito ("ssd" | "hdd" | "any").
    /// </summary>
    [JsonPropertyName("media_type")]
    public string? MediaType { get; init; }
}

public sealed class FolderDetect
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

public sealed class ExplainBlock
{
    [JsonPropertyName("what")]
    public required string What { get; init; }

    [JsonPropertyName("why")]
    public required string Why { get; init; }

    [JsonPropertyName("changes")]
    public required string Changes { get; init; }
}

public sealed class ImpactBlock
{
    [JsonPropertyName("type")]
    public required ImpactType Type { get; init; }

    [JsonPropertyName("confidence")]
    public required Confidence Confidence { get; init; }

    [JsonPropertyName("show_percentage")]
    public bool ShowPercentage { get; init; }
}

