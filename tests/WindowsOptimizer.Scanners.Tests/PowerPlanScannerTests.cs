using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Platform;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

public class PowerPlanScannerTests
{
    private sealed class FakePowerPlanService : IPowerPlanService
    {
        public List<PowerPlanInfo> Plans { get; } = new();
        public string? ActiveGuid { get; set; }

        public Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PowerPlanInfo>>(Plans.Select(p => p with { IsActive = p.Guid == ActiveGuid }).ToList());
        }

        public Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default)
        {
            var plan = Plans.FirstOrDefault(p => p.Guid == ActiveGuid);
            return Task.FromResult(plan != null ? plan with { IsActive = true } : null);
        }

        public Task<bool> SetActivePlanAsync(string planGuid, CancellationToken cancellationToken = default)
        {
            ActiveGuid = planGuid;
            return Task.FromResult(true);
        }

        public Task<PowerPlanInfo?> EnableUltimatePerformanceAsync(CancellationToken cancellationToken = default)
        {
            var existing = Plans.FirstOrDefault(p => p.SchemeType == PowerSchemeType.UltimatePerformance);
            if (existing != null)
            {
                ActiveGuid = existing.Guid;
                return Task.FromResult<PowerPlanInfo?>(existing with { IsActive = true });
            }

            var created = new PowerPlanInfo
            {
                Guid = Guid.NewGuid().ToString(),
                Name = "Máximo rendimiento",
                Description = "Plan de máximo rendimiento",
                IsActive = true,
                IsBuiltIn = false,
                SchemeType = PowerSchemeType.UltimatePerformance
            };
            Plans.Add(created);
            ActiveGuid = created.Guid;
            return Task.FromResult<PowerPlanInfo?>(created);
        }

        public Task<bool> DeletePlanAsync(string planGuid, CancellationToken cancellationToken = default)
        {
            var p = Plans.FirstOrDefault(x => x.Guid == planGuid);
            if (p != null && !p.IsBuiltIn)
            {
                Plans.Remove(p);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    private sealed class FakeRuleProvider : IRuleProvider
    {
        private readonly List<RuleDefinition> _rules;

        public FakeRuleProvider(List<RuleDefinition>? rules = null)
        {
            _rules = rules ?? new List<RuleDefinition>();
        }

        public Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuleDefinition>>(_rules);
        }

        public Task<IReadOnlyList<RuleDefinition>> GetRulesByCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RuleDefinition>>(_rules.Where(r => r.Category == category).ToList());
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ScanAsync_WhenActivePlanIsBalanced_ReturnsDetected()
    {
        var fakeService = new FakePowerPlanService();
        fakeService.Plans.Add(new PowerPlanInfo
        {
            Guid = PowerPlanService.BalancedGuid,
            Name = "Equilibrado",
            IsActive = true,
            IsBuiltIn = true,
            SchemeType = PowerSchemeType.Balanced
        });
        fakeService.ActiveGuid = PowerPlanService.BalancedGuid;

        var scanner = new PowerPlanScanner(fakeService, new FakeRuleProvider(), Category.Gaming);
        var findings = await scanner.ScanAsync();

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.Status == FindingStatus.Detected);
    }

    [Fact]
    public async Task ScanAsync_WhenActivePlanIsHighPerformance_ReturnsAlreadyOptimal()
    {
        var fakeService = new FakePowerPlanService();
        fakeService.Plans.Add(new PowerPlanInfo
        {
            Guid = PowerPlanService.HighPerformanceGuid,
            Name = "Alto rendimiento",
            IsActive = true,
            IsBuiltIn = true,
            SchemeType = PowerSchemeType.HighPerformance
        });
        fakeService.ActiveGuid = PowerPlanService.HighPerformanceGuid;

        var scanner = new PowerPlanScanner(fakeService, new FakeRuleProvider(), Category.Gaming);
        var findings = await scanner.ScanAsync();

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(FindingStatus.AlreadyOptimal, f.Status));
    }

    [Fact]
    public async Task ScanAsync_WhenActivePlanIsUltimatePerformance_ReturnsAlreadyOptimal()
    {
        var fakeService = new FakePowerPlanService();
        string ultimateGuid = Guid.NewGuid().ToString();
        fakeService.Plans.Add(new PowerPlanInfo
        {
            Guid = ultimateGuid,
            Name = "Máximo rendimiento",
            IsActive = true,
            IsBuiltIn = false,
            SchemeType = PowerSchemeType.UltimatePerformance
        });
        fakeService.ActiveGuid = ultimateGuid;

        var scanner = new PowerPlanScanner(fakeService, new FakeRuleProvider(), Category.Gaming);
        var findings = await scanner.ScanAsync();

        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(FindingStatus.AlreadyOptimal, f.Status));
    }

    [Fact]
    public async Task FixAction_Apply_SwitchesToUltimatePerformance_AndCanRevert()
    {
        var fakeService = new FakePowerPlanService();
        fakeService.Plans.Add(new PowerPlanInfo
        {
            Guid = PowerPlanService.BalancedGuid,
            Name = "Equilibrado",
            IsActive = true,
            IsBuiltIn = true,
            SchemeType = PowerSchemeType.Balanced
        });
        fakeService.ActiveGuid = PowerPlanService.BalancedGuid;

        var rule = new RuleDefinition
        {
            Id = "gaming.powerplan-high-performance",
            Name = "Activar Plan de Alto o Máximo Rendimiento",
            Category = Category.Gaming,
            Risk = RiskLevel.Low,
            Reversible = true,
            RequiresAdmin = false,
            RequiresReboot = false,
            Sources = new List<string>(),
            Apply = new List<ApplyStep>
            {
                new() { Type = "power_plan_set", TargetScheme = "UltimatePerformance" }
            }
        };

        var fixAction = new PowerPlanFixAction(fakeService, rule);
        var record = await fixAction.ApplyAsync();

        Assert.Equal(PowerPlanService.BalancedGuid, record.OriginalValue);
        Assert.NotEqual(PowerPlanService.BalancedGuid, fakeService.ActiveGuid);

        // Revert
        await fixAction.RevertAsync(record);
        Assert.Equal(PowerPlanService.BalancedGuid, fakeService.ActiveGuid);
    }

    [Theory]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e", "Equilibrado", PowerSchemeType.Balanced)]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "Alto rendimiento", PowerSchemeType.HighPerformance)]
    [InlineData("a1841308-3541-4fab-bc81-f71556f20b4a", "Economizador", PowerSchemeType.PowerSaver)]
    [InlineData("e9a42b02-d5df-448d-aa00-03f14749eb61", "Máximo rendimiento", PowerSchemeType.UltimatePerformance)]
    public void ClassifyScheme_IdentifiesStandardSchemesCorrectly(string schemeId, string name, PowerSchemeType expected)
    {
        var classified = PowerPlanService.ClassifyScheme(schemeId, name);
        Assert.Equal(expected, classified);
    }

    [Fact]
    public void IsBuiltInGuid_ReturnsTrueForStandardSchemes()
    {
        Assert.True(PowerPlanService.IsBuiltInGuid(PowerPlanService.BalancedGuid));
        Assert.True(PowerPlanService.IsBuiltInGuid(PowerPlanService.HighPerformanceGuid));
        Assert.True(PowerPlanService.IsBuiltInGuid(PowerPlanService.PowerSaverGuid));
        Assert.True(PowerPlanService.IsBuiltInGuid(PowerPlanService.UltimatePerformanceTemplateGuid));
        Assert.False(PowerPlanService.IsBuiltInGuid(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task RealPowerPlanService_DiagnosticCheck()
    {
        var service = new PowerPlanService();
        var plans = await service.GetPowerPlansAsync();
        Assert.NotEmpty(plans);
        var active = await service.GetActivePlanAsync();
        Assert.NotNull(active);
        Assert.True(active.IsActive);
    }
}
