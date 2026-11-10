using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Platform;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

public class ScheduledTaskScannerTests
{
    private sealed class FakeScheduledTaskService : IScheduledTaskService
    {
        public Dictionary<string, ScheduledTaskState> Tasks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ScheduledTaskState> GetTaskStateAsync(string taskPath, CancellationToken cancellationToken = default)
        {
            if (Tasks.TryGetValue(taskPath, out var state))
            {
                return Task.FromResult(state);
            }
            return Task.FromResult(ScheduledTaskState.NotFound);
        }

        public Task<bool> SetTaskStateAsync(string taskPath, ScheduledTaskState state, CancellationToken cancellationToken = default)
        {
            Tasks[taskPath] = state;
            return Task.FromResult(true);
        }

        public Task<bool> TaskExistsAsync(string taskPath, CancellationToken cancellationToken = default)
        {
            bool exists = Tasks.ContainsKey(taskPath) && Tasks[taskPath] != ScheduledTaskState.NotFound;
            return Task.FromResult(exists);
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

    private static RuleDefinition CreateTaskRule(string id, string taskPath, string expectedState = "Disabled")
    {
        return new RuleDefinition
        {
            Id = id,
            Name = "Desactivar " + id,
            Category = Category.Privacy,
            Risk = RiskLevel.Low,
            Presets = [Preset.Standard, Preset.Advanced],
            Reversible = true,
            RequiresAdmin = true,
            RequiresReboot = false,
            Detect = new DetectBlock
            {
                ScheduledTask = new ScheduledTaskDetect
                {
                    Path = taskPath,
                    ExpectedState = expectedState
                }
            },
            Apply =
            [
                new ApplyStep
                {
                    Type = "scheduled_task",
                    Path = taskPath,
                    State = "Disabled"
                }
            ],
            Explain = new ExplainBlock
            {
                What = "Tarea de prueba",
                Why = "Pruebas unitarias",
                Changes = "Deshabilita la tarea"
            }
        };
    }

    [Fact]
    public async Task ScanAsync_WhenTaskIsEnabled_DetectsFinding()
    {
        var taskService = new FakeScheduledTaskService();
        taskService.Tasks[@"\Microsoft\Windows\Autochk\Proxy"] = ScheduledTaskState.Enabled;

        var rule = CreateTaskRule("tasks.autochk.proxy", @"\Microsoft\Windows\Autochk\Proxy");
        var ruleProvider = new FakeRuleProvider([rule]);

        var scanner = new ScheduledTaskScanner(ruleProvider, taskService, Category.Privacy);
        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal("tasks.autochk.proxy", findings[0].Id);
        Assert.Equal(FindingStatus.Detected, findings[0].Status);
    }

    [Fact]
    public async Task ScanAsync_WhenTaskIsDisabled_MarksAlreadyOptimal()
    {
        var taskService = new FakeScheduledTaskService();
        taskService.Tasks[@"\Microsoft\Windows\Autochk\Proxy"] = ScheduledTaskState.Disabled;

        var rule = CreateTaskRule("tasks.autochk.proxy", @"\Microsoft\Windows\Autochk\Proxy");
        var ruleProvider = new FakeRuleProvider([rule]);

        var scanner = new ScheduledTaskScanner(ruleProvider, taskService, Category.Privacy);
        var findings = await scanner.ScanAsync();

        Assert.Single(findings);
        Assert.Equal(FindingStatus.AlreadyOptimal, findings[0].Status);
    }

    [Fact]
    public async Task ScanAsync_WhenTaskNotFound_OmitsFinding()
    {
        var taskService = new FakeScheduledTaskService();
        // No agregamos la tarea a Tasks -> NotFound

        var rule = CreateTaskRule("tasks.autochk.proxy", @"\Microsoft\Windows\Autochk\Proxy");
        var ruleProvider = new FakeRuleProvider([rule]);

        var scanner = new ScheduledTaskScanner(ruleProvider, taskService, Category.Privacy);
        var findings = await scanner.ScanAsync();

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FixAction_AppliesDisabled_AndRollbacksToOriginalState()
    {
        var taskService = new FakeScheduledTaskService();
        taskService.Tasks[@"\Microsoft\Windows\Autochk\Proxy"] = ScheduledTaskState.Enabled;

        var rule = CreateTaskRule("tasks.autochk.proxy", @"\Microsoft\Windows\Autochk\Proxy");

        var fixAction = new ScheduledTaskFixAction(taskService, rule);

        // 1. Aplicar corrección
        var changeRecord = await fixAction.ApplyAsync();

        Assert.Equal("scheduled_task_set", changeRecord.ActionType);
        Assert.Equal(@"\Microsoft\Windows\Autochk\Proxy", changeRecord.Target);
        Assert.Equal("Enabled", changeRecord.OriginalValue);
        Assert.Equal("Disabled", changeRecord.AppliedValue);
        Assert.Equal(ScheduledTaskState.Disabled, taskService.Tasks[@"\Microsoft\Windows\Autochk\Proxy"]);

        // 2. Revertir corrección
        await fixAction.RevertAsync(changeRecord);

        Assert.Equal(ScheduledTaskState.Enabled, taskService.Tasks[@"\Microsoft\Windows\Autochk\Proxy"]);
    }
}
