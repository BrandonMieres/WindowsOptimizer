using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Measurement;

namespace WindowsOptimizer.Measurement.Tests;

public class ImpactEstimatorTests
{
    private readonly ImpactEstimator _estimator = new();

    [Fact]
    public void EstimateImpact_EmptyFindings_ReturnsEmptyEstimate()
    {
        var estimate = _estimator.EstimateImpact(Array.Empty<Finding>());

        Assert.Equal(0, estimate.TotalRecoverableDiskBytes);
        Assert.Equal(0, estimate.TotalOptimizationsCount);
        Assert.Equal(0, estimate.EstimatedBootTimeReductionPercentageMin);
        Assert.Equal(0, estimate.EstimatedBootTimeReductionPercentageMax);
        Assert.Contains("No hay optimizaciones", estimate.SummaryDescription);
    }

    [Fact]
    public void EstimateImpact_DiskCleaners_AggregatesExactBytes()
    {
        var findings = new List<Finding>
        {
            CreateFinding("cleaner.temp", Category.Cleaners, ImpactType.DiskSpace, 500L * 1024 * 1024),
            CreateFinding("cleaner.cache", Category.Cleaners, ImpactType.DiskSpace, 1500L * 1024 * 1024)
        };

        var estimate = _estimator.EstimateImpact(findings);

        Assert.Equal(2000L * 1024 * 1024, estimate.TotalRecoverableDiskBytes);
        Assert.Matches(@"~2[.,]0 GB de espacio", estimate.SummaryDescription);
        Assert.Equal(2, estimate.TotalOptimizationsCount);
    }

    [Fact]
    public void EstimateImpact_StartupItems_CalculatesBootTimeReductionWithBaseline()
    {
        var baseline = new SystemBaseline
        {
            BootDuration = TimeSpan.FromSeconds(40),
            TotalRamBytes = 16UL * 1024 * 1024 * 1024,
            AvailableRamBytes = 8UL * 1024 * 1024 * 1024,
            ProcessCount = 150,
            SystemUptime = TimeSpan.FromHours(5),
            SystemDriveFreeBytes = 50UL * 1024 * 1024 * 1024,
            SystemDriveTotalBytes = 500UL * 1024 * 1024 * 1024
        };

        var findings = new List<Finding>
        {
            CreateFinding("startup.cortana", Category.Startup, ImpactType.BootTime),
            CreateFinding("startup.edge", Category.Startup, ImpactType.BootTime),
            CreateFinding("startup.teams", Category.Startup, ImpactType.BootTime)
        };

        var estimate = _estimator.EstimateImpact(findings, baseline);

        // 3 startup items: min = 3 * 2 = 6%, max = 3 * 4.5 = 13.5%
        Assert.Equal(6.0, estimate.EstimatedBootTimeReductionPercentageMin);
        Assert.Equal(13.5, estimate.EstimatedBootTimeReductionPercentageMax);

        // 40s * 6% = 2.4s, 40s * 13.5% = 5.4s
        Assert.NotNull(estimate.EstimatedBootTimeReductionMin);
        Assert.NotNull(estimate.EstimatedBootTimeReductionMax);
        Assert.True(estimate.EstimatedBootTimeReductionMin.Value.TotalSeconds >= 2.0);
        Assert.True(estimate.EstimatedBootTimeReductionMax.Value.TotalSeconds <= 6.0);
        Assert.Matches(@"arranque ~2[.,]4-5[.,]4s más rápido", estimate.SummaryDescription);
    }

    [Fact]
    public void EstimateImpact_MixedCategories_CategorizesCorrectly()
    {
        var findings = new List<Finding>
        {
            CreateFinding("cleaner.win", Category.Cleaners, ImpactType.DiskSpace, 100L * 1024 * 1024),
            CreateFinding("privacy.telemetry", Category.Privacy, ImpactType.Privacy),
            CreateFinding("privacy.cortana", Category.Privacy, ImpactType.Privacy),
            CreateFinding("perf.gamebar", Category.Gaming, ImpactType.Performance),
            CreateFinding("perf.gpu", Category.Performance, ImpactType.Performance),
            CreateFinding("system.longpaths", Category.System, ImpactType.Productivity),
            CreateFinding("ui.darkmode", Category.Ui, ImpactType.Productivity)
        };

        var estimate = _estimator.EstimateImpact(findings);

        Assert.Equal(7, estimate.TotalOptimizationsCount);
        Assert.Equal(2, estimate.PrivacyTweaksCount);
        Assert.Equal(2, estimate.PerformanceTweaksCount);
        Assert.Equal(2, estimate.SystemTweaksCount);
        Assert.Equal(100L * 1024 * 1024, estimate.TotalRecoverableDiskBytes);
        Assert.Contains("~100 MB de espacio", estimate.SummaryDescription);
        Assert.Contains("2 ajustes de privacidad", estimate.SummaryDescription);
    }

    private static Finding CreateFinding(string id, Category category, ImpactType impactType, long? absoluteValue = null)
    {
        return new Finding
        {
            Id = id,
            Name = id,
            Category = category,
            Severity = Severity.Medium,
            Risk = RiskLevel.Low,
            Status = FindingStatus.Detected,
            IsReversible = true,
            Explanation = new FindingExplanation
            {
                What = "Test what",
                Why = "Test why",
                Changes = "Test changes"
            },
            Impact = new ImpactEstimate
            {
                Type = impactType,
                Confidence = Confidence.High,
                ShowPercentage = true,
                AbsoluteValue = absoluteValue,
                Unit = absoluteValue.HasValue ? "bytes" : null
            }
        };
    }
}
