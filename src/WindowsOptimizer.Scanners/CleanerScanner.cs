using System.Runtime.Versioning;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Core.Utilities;

namespace WindowsOptimizer.Scanners;

[SupportedOSPlatform("windows")]
public sealed class CleanerScanner : IScanner
{
    private readonly IRuleProvider _ruleProvider;
    private readonly IRollbackStore _rollbackStore;

    public string Name => "Cleaner Scanner";
    public Category Category => Category.Cleaners;

    public CleanerScanner(IRuleProvider ruleProvider, IRollbackStore rollbackStore)
    {
        _ruleProvider = ruleProvider;
        _rollbackStore = rollbackStore;
    }

    public async Task<IReadOnlyList<Finding>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _ruleProvider.GetRulesAsync(cancellationToken);
        var folderRules = rules.Where(r => r.Detect?.Folder is not null);
        var findings = new List<Finding>();
        
        var history = await _rollbackStore.GetHistoryAsync(cancellationToken);

        foreach (var rule in folderRules)
        {
            var detect = rule.Detect!.Folder!;
            var expandedPaths = VariableExpander.ExpandPaths(detect.Path);

            FindingStatus status = FindingStatus.Unknown;
            long sizeBytes = 0;
            bool foundAny = false;

            foreach (var expandedPath in expandedPaths)
            {
                try
                {
                    if (Directory.Exists(expandedPath))
                    {
                        var files = Directory.GetFiles(expandedPath, "*", SearchOption.TopDirectoryOnly); // VariableExpander already handles recursion logic for detection
                        if (files.Length > 0)
                        {
                            foundAny = true;
                            foreach (var file in files)
                            {
                                try { sizeBytes += new FileInfo(file).Length; }
                                catch { }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore access exceptions for individual paths during scanning
                }
            }

            if (foundAny)
            {
                status = FindingStatus.Detected;
            }
            else
            {
                status = FindingStatus.AlreadyOptimal;
            }

            // 7-day Cooldown Logic
            var lastClean = history
                .Where(h => h.RuleId == rule.Id)
                .OrderByDescending(h => h.AppliedAt)
                .FirstOrDefault();

            string currentValText = sizeBytes > 0 ? $"{sizeBytes / 1024 / 1024} MB" : "Limpio";

            if (lastClean != null && (DateTimeOffset.UtcNow - lastClean.AppliedAt).TotalDays < 7)
            {
                status = FindingStatus.AlreadyOptimal;
                var days = (int)(DateTimeOffset.UtcNow - lastClean.AppliedAt).TotalDays;
                string dayStr = days == 0 ? "hoy" : $"hace {days} días";
                currentValText = $"Limpieza reciente ({dayStr})";
            }

            var desc = rule.Description;
            if (sizeBytes > 0 && status != FindingStatus.AlreadyOptimal)
            {
                desc = $"{desc} (Aprox {sizeBytes / 1024 / 1024} MB recuperables)";
            }
            else if (status == FindingStatus.AlreadyOptimal && lastClean != null && (DateTimeOffset.UtcNow - lastClean.AppliedAt).TotalDays < 7)
            {
                var days = (int)(DateTimeOffset.UtcNow - lastClean.AppliedAt).TotalDays;
                string dayStr = days == 0 ? "hoy" : $"hace {days} días";
                desc = $"{desc} (Limpieza reciente: {dayStr})";
            }

            var severity = rule.Risk switch
            {
                RiskLevel.Low => Severity.Medium,
                RiskLevel.Medium => Severity.High,
                RiskLevel.High => Severity.Critical,
                _ => Severity.Low
            };

            findings.Add(new Finding
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = desc ?? string.Empty,
                Category = rule.Category,
                Severity = severity,
                Risk = rule.Risk,
                IsReversible = rule.Reversible,
                RequiresAdmin = rule.RequiresAdmin,
                RequiresReboot = rule.RequiresReboot,
                Status = status,
                CurrentValue = currentValText,
                RecommendedValue = "0 MB",
                Sources = rule.Sources,
                Presets = rule.Presets,
                Explanation = rule.Explain is not null ? new FindingExplanation { What = rule.Explain.What, Why = rule.Explain.Why, Changes = rule.Explain.Changes } : new FindingExplanation { What = rule.Name, Why = rule.Description ?? "", Changes = "" },
                Impact = rule.Impact is not null 
                    ? new ImpactEstimate { Type = rule.Impact.Type, Confidence = rule.Impact.Confidence, ShowPercentage = rule.Impact.ShowPercentage, AbsoluteValue = sizeBytes > 0 ? sizeBytes : null, Unit = "bytes" } 
                    : new ImpactEstimate { Type = ImpactType.DiskSpace, Confidence = Confidence.High, ShowPercentage = false, AbsoluteValue = sizeBytes > 0 ? sizeBytes : null, Unit = "bytes" }
            });
        }
        return findings.AsReadOnly();
    }
}
