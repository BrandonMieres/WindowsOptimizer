using System.Text.Json;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Analizador genérico que evalúa reglas de registro filtradas por categoría.
/// Permite instanciar uno por categoría (Privacy, Startup, Network, etc.) sin duplicar lógica.
/// </summary>
public sealed class RegistryScanner : IScanner
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IRegistryAccess _registry;

    public string Name => $"Registry Scanner ({Category})";
    public Category Category { get; }

    public RegistryScanner(IRuleProvider ruleProvider, IRegistryAccess registry, Category category = Category.Privacy)
    {
        _ruleProvider = ruleProvider;
        _registry = registry;
        Category = category;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);

        // Solo reglas que tengan detección por registro Y coincidan con nuestra categoría
        var registryRules = rules.Where(r =>
            r.Detect?.Registry is not null &&
            r.Category == Category);

        var findings = new List<Finding>();

        foreach (var rule in registryRules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detect = rule.Detect!.Registry!;
            object? currentValue = null;

            try
            {
                currentValue = _registry.GetValue(detect.Path, detect.Name);
            }
            catch (Exception)
            {
                // Clave de registro inaccesible — tratar como "no óptimo"
            }

            bool isOptimal = EvaluateOptimality(currentValue, detect);

            // Determinar el valor recomendado: expected_value si existe, o el value del primer apply
            object? recommendedValue = detect.ExpectedValue;
            if (recommendedValue is null && rule.Apply.Count > 0)
            {
                recommendedValue = ParseJsonValue(rule.Apply[0].Value);
            }

            // Mapear riesgo a severidad
            var severity = rule.Risk switch
            {
                RiskLevel.Low    => Severity.Low,
                RiskLevel.Medium => Severity.Medium,
                RiskLevel.High   => Severity.High,
                _                => Severity.Info
            };

            findings.Add(new Finding
            {
                Id              = rule.Id,
                Name            = rule.Name,
                Description     = rule.Description,
                Category        = rule.Category,
                Severity        = severity,
                Risk            = rule.Risk,
                IsReversible    = rule.Reversible,
                RequiresAdmin   = rule.RequiresAdmin,
                RequiresReboot  = rule.RequiresReboot,
                Status          = isOptimal ? FindingStatus.AlreadyOptimal : FindingStatus.Detected,
                CurrentValue    = currentValue,
                RecommendedValue = recommendedValue is JsonElement je ? ParseJsonValue(je) : recommendedValue,
                Sources         = rule.Sources,
                Presets         = rule.Presets,
                Explanation     = rule.Explain is not null
                    ? new FindingExplanation
                    {
                        What    = rule.Explain.What,
                        Why     = rule.Explain.Why,
                        Changes = rule.Explain.Changes
                    }
                    : new FindingExplanation
                    {
                        What    = rule.Name,
                        Why     = rule.Description ?? "",
                        Changes = ""
                    },
                Impact = rule.Impact is not null
                    ? new ImpactEstimate
                    {
                        Type           = rule.Impact.Type,
                        Confidence     = rule.Impact.Confidence,
                        ShowPercentage = rule.Impact.ShowPercentage
                    }
                    : new ImpactEstimate
                    {
                        Type           = ImpactType.None,
                        Confidence     = Confidence.Low,
                        ShowPercentage = false
                    }
            });
        }

        return findings.AsReadOnly();
    }

    /// <summary>
    /// Determina si el valor actual del sistema ya es óptimo.
    ///   - Si hay expected_value: óptimo si current == expected
    ///   - Si hay bad_value: óptimo si current != bad
    ///   - Si no hay criterio: no óptimo (forzar detección)
    /// </summary>
    private static bool EvaluateOptimality(object? currentValue, RegistryDetect detect)
    {
        if (detect.ExpectedValue is not null)
        {
            var expected = ParseJsonValue(detect.ExpectedValue);
            return ValuesMatch(currentValue, expected);
        }

        if (detect.BadValue is not null)
        {
            var bad = ParseJsonValue(detect.BadValue);
            return !ValuesMatch(currentValue, bad);
        }

        return false;
    }

    private static object? ParseJsonValue(object? jsonValue)
    {
        if (jsonValue is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetInt64(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True   => 1,
                JsonValueKind.False  => 0,
                _                    => null
            };
        }
        return jsonValue;
    }

    private static bool ValuesMatch(object? current, object? expected)
    {
        if (current is null && expected is null) return true;
        if (current is null || expected is null) return false;

        // Normalizar a string para comparar int/long/string uniformemente
        return current.ToString()!.Equals(expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
