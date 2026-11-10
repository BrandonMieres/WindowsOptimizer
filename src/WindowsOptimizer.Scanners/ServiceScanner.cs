using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

/// <summary>
/// Analizador que evalúa reglas basadas en servicios de Windows.
/// Soporta la propiedad <c>ssd_only</c> para omitir recomendaciones
/// incompatibles con el tipo de media del disco del sistema.
/// </summary>
public sealed class ServiceScanner : IScanner
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IServiceManager _serviceManager;
    private readonly IDriveMediaService? _driveMediaService;

    public string Name => "Service Scanner";
    public Category Category => Category.Services;

    public ServiceScanner(
        IRuleProvider ruleProvider,
        IServiceManager serviceManager,
        IDriveMediaService? driveMediaService = null)
    {
        _ruleProvider = ruleProvider;
        _serviceManager = serviceManager;
        _driveMediaService = driveMediaService;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);

        // Solo reglas con detección por servicio
        var serviceRules = rules.Where(r => r.Detect?.Service is not null).ToList();
        var findings = new List<Finding>();

        // Detectamos el tipo de media una sola vez para toda la pasada
        bool? isSsd = null;
        if (_driveMediaService is not null && serviceRules.Any(r => r.SsdOnly || r.Detect?.Service?.MediaType is not null))
        {
            isSsd = await _driveMediaService.IsSystemDriveSolidStateAsync(cancellationToken);
        }

        foreach (var rule in serviceRules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detect = rule.Detect!.Service!;

            // ── Filtro por tipo de media ─────────────────────────────────────
            // ssd_only: true  → omitir en HDD (Superfetch/WSearch son útiles en HDD)
            if (rule.SsdOnly && isSsd.HasValue && !isSsd.Value)
            {
                // No mostramos la regla: en HDD SysMain/WSearch ayudan más de lo que perjudican
                continue;
            }

            // detect.service.media_type: "ssd" | "hdd" | "any"
            if (detect.MediaType is not null)
            {
                var requirement = detect.MediaType.ToLowerInvariant();
                if (requirement == "ssd" && isSsd.HasValue && !isSsd.Value)
                    continue;
                if (requirement == "hdd" && isSsd.HasValue && isSsd.Value)
                    continue;
            }
            // ────────────────────────────────────────────────────────────────

            var serviceInfo = _serviceManager.GetServiceInfo(detect.Name);
            if (serviceInfo is null)
            {
                continue; // servicio no existe en este sistema
            }

            bool isOptimal = EvaluateOptimality(serviceInfo, detect);

            var severity = rule.Risk switch
            {
                RiskLevel.Low    => Severity.Medium,
                RiskLevel.Medium => Severity.High,
                RiskLevel.High   => Severity.Critical,
                _                => Severity.Low
            };

            // Enriquecer la explicación con contexto de media si aplica
            var explanation = BuildExplanation(rule, isSsd);

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
                CurrentValue    = $"{serviceInfo.StartType} ({serviceInfo.Status})",
                RecommendedValue = GetRecommendedFromRule(rule),
                Sources         = rule.Sources,
                Presets         = rule.Presets,
                Explanation     = explanation,
                Impact          = rule.Impact is not null
                    ? new ImpactEstimate
                    {
                        Type            = rule.Impact.Type,
                        Confidence      = rule.Impact.Confidence,
                        ShowPercentage  = rule.Impact.ShowPercentage
                    }
                    : new ImpactEstimate
                    {
                        Type           = ImpactType.Performance,
                        Confidence     = Confidence.Low,
                        ShowPercentage = false
                    }
            });
        }

        return findings.AsReadOnly();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool EvaluateOptimality(ServiceInfo info, ServiceDetect detect)
    {
        if (detect.BadStartType is not null)
        {
            if (Enum.TryParse<ServiceStartMode>(detect.BadStartType, true, out var badMode))
                return info.StartType != badMode;
        }

        if (detect.ExpectedStartType is not null)
        {
            if (Enum.TryParse<ServiceStartMode>(detect.ExpectedStartType, true, out var expectedMode))
                return info.StartType == expectedMode;
        }

        return false;
    }

    private static string? GetRecommendedFromRule(RuleDefinition rule)
    {
        var step = rule.Apply.FirstOrDefault(s =>
            s.Type.Equals("service_set", StringComparison.OrdinalIgnoreCase));
        return step?.StartType;
    }

    private static FindingExplanation BuildExplanation(RuleDefinition rule, bool? isSsd)
    {
        if (rule.Explain is null)
        {
            return new FindingExplanation
            {
                What    = rule.Name,
                Why     = rule.Description ?? string.Empty,
                Changes = string.Empty
            };
        }

        string why = rule.Explain.Why;

        // Añadir contexto de media si la regla es ssd_only
        if (rule.SsdOnly && isSsd.HasValue)
        {
            why += isSsd.Value
                ? " ✓ Tu disco es SSD: esta optimización es segura y recomendada."
                : " ⚠ Esta regla solo aplica a discos SSD.";
        }

        return new FindingExplanation
        {
            What    = rule.Explain.What,
            Why     = why,
            Changes = rule.Explain.Changes
        };
    }
}
