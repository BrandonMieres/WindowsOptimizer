using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Platform;
using Xunit;

namespace WindowsOptimizer.Actions.Tests;

public class AppxFixActionTests
{
    private sealed class FakeAppxService : IAppxService
    {
        public List<AppxPackage> Packages { get; } = new();
        public List<string> RemovedPackages { get; } = new();

        public FakeAppxService(IEnumerable<AppxPackage>? initialPackages = null)
        {
            if (initialPackages != null)
            {
                Packages.AddRange(initialPackages);
            }
        }

        public Task<List<AppxPackage>> GetInstalledAppsAsync()
        {
            return Task.FromResult(new List<AppxPackage>(Packages));
        }

        public Task<bool> RemoveAppAsync(string packageFullName)
        {
            RemovedPackages.Add(packageFullName);
            Packages.RemoveAll(p => p.PackageFullName.Equals(packageFullName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void Properties_MatchRuleDefinition()
    {
        var rule = new RuleDefinition
        {
            Id = "apps.remove-bing-news",
            Name = "Desinstalar Bing News",
            Category = Category.Apps,
            Risk = RiskLevel.Low,
            RequiresAdmin = false
        };

        var appxService = new FakeAppxService();
        var action = new AppxFixAction(appxService, rule);

        Assert.Equal("apps.remove-bing-news", action.RuleId);
        Assert.False(action.RequiresAdmin);
    }

    [Fact]
    public async Task ApplyAsync_WhenAppInstalled_RemovesAppAndReturnsValidRecord()
    {
        var rule = new RuleDefinition
        {
            Id = "apps.remove-bing-news",
            Name = "Desinstalar Bing News",
            Category = Category.Apps,
            Risk = RiskLevel.Low,
            Detect = new DetectBlock
            {
                Appx = new AppxDetect { Name = "Microsoft.BingNews" }
            },
            Apply = new List<ApplyStep>
            {
                new() { Type = "appx_remove", PackageName = "Microsoft.BingNews" }
            }
        };

        var installed = new List<AppxPackage>
        {
            new()
            {
                Name = "Microsoft News",
                PackageFullName = "Microsoft.BingNews_4.53.61201.0_x64__8wekyb3d8bbwe"
            }
        };

        var fakeService = new FakeAppxService(installed);
        var action = new AppxFixAction(fakeService, rule);

        var record = await action.ApplyAsync();

        Assert.NotNull(record);
        Assert.Equal("apps.remove-bing-news", record.RuleId);
        Assert.Equal("appx_remove", record.ActionType);
        Assert.Equal("Microsoft.BingNews_4.53.61201.0_x64__8wekyb3d8bbwe", record.Target);
        Assert.Equal("Removed", record.AppliedValue);
        Assert.Contains("Microsoft.BingNews_4.53.61201.0_x64__8wekyb3d8bbwe", fakeService.RemovedPackages);
    }

    [Fact]
    public async Task RevertAsync_DoesNotThrow()
    {
        var rule = new RuleDefinition
        {
            Id = "apps.remove-weather",
            Name = "Desinstalar Clima",
            Category = Category.Apps,
            Risk = RiskLevel.Low,
            Reversible = false
        };

        var action = new AppxFixAction(new FakeAppxService(), rule);
        var record = new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = rule.Id,
            Name = rule.Name,
            ActionType = "appx_remove",
            Target = "Microsoft.BingWeather_1.0_x64",
            AppliedValue = "Removed",
            AppliedAt = DateTimeOffset.UtcNow
        };

        var exception = await Record.ExceptionAsync(() => action.RevertAsync(record));
        Assert.Null(exception);
    }
}
