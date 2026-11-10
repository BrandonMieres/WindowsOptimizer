using WindowsOptimizer.Platform;

namespace WindowsOptimizer.Actions.Tests;

public class DeviceHealthServiceTests
{
    [Fact]
    public async Task AuditDevicesAndDriversAsync_ReturnsValidSummaryAndCollections()
    {
        var service = new DeviceHealthService();

        var (problems, drivers, summary) = await service.AuditDevicesAndDriversAsync();

        Assert.NotNull(problems);
        Assert.NotNull(drivers);
        Assert.NotNull(summary);
        Assert.True(summary.TotalDevicesCount >= 0);
        Assert.False(string.IsNullOrEmpty(summary.SummaryDescription));

        // If there are drivers, each one should have valid properties
        foreach (var driver in drivers.Take(10))
        {
            Assert.False(string.IsNullOrWhiteSpace(driver.DeviceName));
            Assert.False(string.IsNullOrWhiteSpace(driver.DeviceClass));
            Assert.False(string.IsNullOrWhiteSpace(driver.StatusText));
        }
    }
}
