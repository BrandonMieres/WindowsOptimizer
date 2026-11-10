using WindowsOptimizer.Measurement;

namespace WindowsOptimizer.Measurement.Tests;

public class SystemBaselineServiceTests
{
    [Fact]
    public async Task GetBaselineAsync_ReturnsValidSystemMetrics()
    {
        var service = new SystemBaselineService();

        var baseline = await service.GetBaselineAsync();

        Assert.NotNull(baseline);
        Assert.True(baseline.TotalRamBytes > 0, "Total RAM should be greater than 0");
        Assert.True(baseline.AvailableRamBytes > 0, "Available RAM should be greater than 0");
        Assert.True(baseline.ProcessCount > 0, "Process count should be greater than 0");
        Assert.True(baseline.SystemUptime > TimeSpan.Zero, "Uptime should be greater than 0");
        Assert.True(baseline.SystemDriveTotalBytes > 0, "System drive total size should be greater than 0");
        Assert.True(baseline.RamUsagePercentage is >= 0 and <= 100, "RAM usage percentage must be between 0 and 100");
    }
}
