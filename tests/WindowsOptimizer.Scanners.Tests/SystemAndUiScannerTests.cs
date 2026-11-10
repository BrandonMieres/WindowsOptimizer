using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

public class SystemAndUiScannerTests
{
    private sealed class FakeRegistryAccess : IRegistryAccess
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        public void SetValue(string keyPath, string valueName, object value, string kind)
        {
            _values[$"{keyPath}\\{valueName}"] = value;
        }

        public object? GetValue(string keyPath, string valueName)
        {
            return _values.TryGetValue($"{keyPath}\\{valueName}", out var val) ? val : null;
        }

        public bool ValueExists(string path, string name)
        {
            return _values.ContainsKey($"{path}\\{name}");
        }

        public bool KeyExists(string path)
        {
            return _values.Keys.Any(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase));
        }

        public void DeleteValue(string path, string name)
        {
            _values.Remove($"{path}\\{name}");
        }
    }

    private sealed class FakeRuleProvider : IRuleProvider
    {
        private readonly List<RuleDefinition> _rules;

        public FakeRuleProvider(List<RuleDefinition> rules)
        {
            _rules = rules;
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
    public async Task ScanAsync_SystemCategory_DetectsPendingSystemTweaks()
    {
        var reg = new FakeRegistryAccess();
        // HibernateEnabled = 1 (malo)
        reg.SetValue(@"HKLM\System\CurrentControlSet\Control\Power", "HibernateEnabled", 1, "DWord");

        var rule = new RuleDefinition
        {
            Id = "system.disable-hibernation",
            Name = "Desactivar Hibernación",
            Category = Category.System,
            Risk = RiskLevel.Low,
            Reversible = true,
            Detect = new DetectBlock
            {
                Registry = new RegistryDetect
                {
                    Path = @"HKLM\System\CurrentControlSet\Control\Power",
                    Name = "HibernateEnabled",
                    ExpectedValue = 0
                }
            },
            Apply =
            [
                new ApplyStep
                {
                    Type = "command",
                    Command = "powercfg",
                    Arguments = "-h off"
                }
            ],
            Explain = new ExplainBlock
            {
                What = "Hibernación",
                Why = "Liberar espacio",
                Changes = "powercfg -h off"
            }
        };

        var provider = new FakeRuleProvider([rule]);
        var scanner = new RegistryScanner(provider, reg, Category.System);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal("system.disable-hibernation", findings[0].Id);
        Assert.Equal(Category.System, findings[0].Category);
        Assert.Equal(FindingStatus.Detected, findings[0].Status);
    }

    [Fact]
    public async Task ScanAsync_UiCategory_DetectsPendingUiCustomizations()
    {
        var reg = new FakeRegistryAccess();
        // HideFileExt = 1 (malo, extensiones ocultas)
        reg.SetValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1, "DWord");

        var rule = new RuleDefinition
        {
            Id = "ui.show-file-extensions",
            Name = "Mostrar extensiones de archivos",
            Category = Category.Ui,
            Risk = RiskLevel.Low,
            Reversible = true,
            Detect = new DetectBlock
            {
                Registry = new RegistryDetect
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "HideFileExt",
                    ExpectedValue = 0
                }
            },
            Apply =
            [
                new ApplyStep
                {
                    Type = "registry_set",
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    Name = "HideFileExt",
                    Value = 0,
                    Kind = "DWord"
                }
            ],
            Explain = new ExplainBlock
            {
                What = "Extensiones",
                Why = "Seguridad",
                Changes = "HideFileExt = 0"
            }
        };

        var provider = new FakeRuleProvider([rule]);
        var scanner = new RegistryScanner(provider, reg, Category.Ui);

        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal("ui.show-file-extensions", findings[0].Id);
        Assert.Equal(Category.Ui, findings[0].Category);
        Assert.Equal(FindingStatus.Detected, findings[0].Status);

        // Si ya está óptimo (HideFileExt = 0)
        reg.SetValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, "DWord");
        var findingsOptimal = await scanner.ScanAsync();
        Assert.Single(findingsOptimal);
        Assert.Equal(FindingStatus.AlreadyOptimal, findingsOptimal[0].Status);
    }
}
