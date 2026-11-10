using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

public class AppxScannerTests
{
    private sealed class FakeRuleProvider : IRuleProvider
    {
        private readonly IReadOnlyList<RuleDefinition> _rules;

        public FakeRuleProvider(IReadOnlyList<RuleDefinition> rules)
        {
            _rules = rules;
        }

        public Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rules);
        }

        public Task<IReadOnlyList<RuleDefinition>> GetRulesByCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            var filtered = _rules.Where(r => r.Category == category).ToList();
            return Task.FromResult<IReadOnlyList<RuleDefinition>>(filtered);
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppxService : IAppxService
    {
        private readonly List<AppxPackage> _packages;

        public FakeAppxService(List<AppxPackage> packages)
        {
            _packages = packages;
        }

        public Task<List<AppxPackage>> GetInstalledAppsAsync()
        {
            return Task.FromResult(_packages);
        }

        public Task<bool> RemoveAppAsync(string packageFullName)
        {
            _packages.RemoveAll(p => p.PackageFullName.Equals(packageFullName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task ScanAsync_WhenNoAppRules_ReturnsEmptyList()
    {
        var rules = new List<RuleDefinition>
        {
            new()
            {
                Id = "privacy.telemetry",
                Name = "Desactivar Telemetría",
                Category = Category.Privacy,
                Risk = RiskLevel.Low
            }
        };

        var ruleProvider = new FakeRuleProvider(rules);
        var appxService = new FakeAppxService(new List<AppxPackage>());
        var scanner = new AppxScanner(ruleProvider, appxService);

        var findings = await scanner.ScanAsync();

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ScanAsync_WhenAppIsInstalled_ReturnsFindingDetected()
    {
        var rules = new List<RuleDefinition>
        {
            new()
            {
                Id = "apps.remove-bing-news",
                Name = "Desinstalar Bing News",
                Description = "Noticias de MSN",
                Category = Category.Apps,
                Risk = RiskLevel.Low,
                Presets = new List<Preset> { Preset.Minimal, Preset.Standard },
                Detect = new DetectBlock
                {
                    Appx = new AppxDetect { Name = "Microsoft.BingNews" }
                },
                Apply = new List<ApplyStep>
                {
                    new() { Type = "appx_remove", PackageName = "Microsoft.BingNews" }
                },
                Explain = new ExplainBlock
                {
                    What = "Noticias de MSN",
                    Why = "Consume ancho de banda",
                    Changes = "Desinstala paquete"
                },
                Impact = new ImpactBlock
                {
                    Type = ImpactType.DiskSpace,
                    Confidence = Confidence.High,
                    ShowPercentage = false
                }
            }
        };

        var installedPackages = new List<AppxPackage>
        {
            new()
            {
                Name = "Microsoft News",
                PackageFullName = "Microsoft.BingNews_4.53.61201.0_x64__8wekyb3d8bbwe",
                Publisher = "Microsoft Corporation",
                Version = "4.53.61201.0"
            }
        };

        var scanner = new AppxScanner(new FakeRuleProvider(rules), new FakeAppxService(installedPackages));
        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        var finding = findings[0];
        Assert.Equal("apps.remove-bing-news", finding.Id);
        Assert.Equal(FindingStatus.Detected, finding.Status);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(RiskLevel.Low, finding.Risk);
        Assert.Contains(Preset.Minimal, finding.Presets);
        Assert.NotNull(finding.Impact);
        Assert.Equal(ImpactType.DiskSpace, finding.Impact.Type);
        Assert.Equal("Noticias de MSN", finding.Explanation?.What);
    }

    [Fact]
    public async Task ScanAsync_WhenAppIsNotInstalled_ReturnsFindingAlreadyOptimal()
    {
        var rules = new List<RuleDefinition>
        {
            new()
            {
                Id = "apps.remove-weather",
                Name = "Desinstalar Clima",
                Category = Category.Apps,
                Risk = RiskLevel.Low,
                Detect = new DetectBlock
                {
                    Appx = new AppxDetect { Name = "Microsoft.BingWeather" }
                }
            }
        };

        var installedPackages = new List<AppxPackage>
        {
            new()
            {
                Name = "Calculadora",
                PackageFullName = "Microsoft.WindowsCalculator_10.0.0.0_x64__8wekyb3d8bbwe"
            }
        };

        var scanner = new AppxScanner(new FakeRuleProvider(rules), new FakeAppxService(installedPackages));
        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal(FindingStatus.AlreadyOptimal, findings[0].Status);
    }

    [Fact]
    public async Task ScanAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var rules = new List<RuleDefinition>
        {
            new()
            {
                Id = "apps.rule",
                Name = "App de Prueba",
                Category = Category.Apps,
                Risk = RiskLevel.Low,
                Detect = new DetectBlock { Appx = new AppxDetect { Name = "TestApp" } }
            }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var scanner = new AppxScanner(new FakeRuleProvider(rules), new FakeAppxService(new List<AppxPackage>()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync(cts.Token));
    }
}
