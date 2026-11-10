using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

public sealed class AppxScanner : IScanner
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IAppxService _appxService;

    public string Name => "Appx Bloatware Scanner";
    public Category Category => Category.Apps;

    public AppxScanner(IRuleProvider ruleProvider, IAppxService appxService)
    {
        _ruleProvider = ruleProvider;
        _appxService = appxService;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var appRules = rules.Where(r => r.Category == Category.Apps && r.Detect?.Appx is not null).ToList();

        if (appRules.Count == 0)
        {
            return Array.Empty<Finding>();
        }

        var installedApps = await _appxService.GetInstalledAppsAsync();
        var findings = new List<Finding>();

        foreach (var rule in appRules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pattern = rule.Detect!.Appx!.Name;
            var isInstalled = installedApps.Any(a => 
                a.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                a.PackageFullName.Contains(pattern, StringComparison.OrdinalIgnoreCase));

            var severity = rule.Risk switch
            {
                RiskLevel.Low => Severity.Low,
                RiskLevel.Medium => Severity.Medium,
                RiskLevel.High => Severity.High,
                _ => Severity.Low
            };

            var impact = rule.Impact != null ? new ImpactEstimate
            {
                Type = rule.Impact.Type,
                Confidence = rule.Impact.Confidence,
                ShowPercentage = rule.Impact.ShowPercentage
            } : new ImpactEstimate
            {
                Type = ImpactType.DiskSpace,
                Confidence = Confidence.High,
                ShowPercentage = false
            };

            var explanation = rule.Explain != null ? new FindingExplanation
            {
                What = rule.Explain.What,
                Why = rule.Explain.Why,
                Changes = rule.Explain.Changes
            } : new FindingExplanation
            {
                What = rule.Name,
                Why = rule.Description ?? string.Empty,
                Changes = "Desinstala el paquete AppX de la aplicación."
            };

            findings.Add(new Finding
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                Category = rule.Category,
                Severity = severity,
                Risk = rule.Risk,
                Presets = rule.Presets.ToList(),
                Status = isInstalled ? FindingStatus.Detected : FindingStatus.AlreadyOptimal,
                IsReversible = rule.Reversible,
                Impact = impact,
                Explanation = explanation
            });
        }

        return findings.AsReadOnly();
    }
}
