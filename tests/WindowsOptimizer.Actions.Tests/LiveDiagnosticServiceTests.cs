using System.Diagnostics;
using WindowsOptimizer.Platform;

namespace WindowsOptimizer.Actions.Tests;

public class LiveDiagnosticServiceTests
{
    [Fact]
    public async Task CaptureSnapshotAsync_ReturnsValidSnapshotAndMetrics()
    {
        var service = new LiveDiagnosticService();

        var snapshot = await service.CaptureSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalRamBytes > 0, "Total RAM should be greater than 0");
        Assert.True(snapshot.TotalRamPercentage >= 0 && snapshot.TotalRamPercentage <= 100);
        Assert.True(snapshot.TopMemoryProcesses.Count > 0, "Should detect active processes using memory");
        Assert.True(snapshot.TopCpuProcesses.Count > 0, "Should detect active processes using CPU");

        // Verify each process resource info
        var firstProc = snapshot.TopMemoryProcesses[0];
        Assert.True(firstProc.Pid > 0);
        Assert.False(string.IsNullOrWhiteSpace(firstProc.ProcessName));
        Assert.False(string.IsNullOrWhiteSpace(firstProc.MemoryDisplay));
        Assert.False(string.IsNullOrWhiteSpace(firstProc.DiagnosisNote));
    }

    [Fact]
    public async Task TerminateProcessAsync_ProtectedProcess_CannotBeTerminated()
    {
        var service = new LiveDiagnosticService();

        // PID 4 is always System on Windows
        bool result = await service.TerminateProcessAsync(4);

        Assert.False(result, "System critical process PID 4 must never be terminated");
    }

    [Fact]
    public async Task CaptureSnapshotAsync_ReturnsValidProcessGroups()
    {
        var service = new LiveDiagnosticService();

        var snapshot = await service.CaptureSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot.TopMemoryGroups);
        Assert.NotEmpty(snapshot.TopCpuGroups);

        var firstGroup = snapshot.TopMemoryGroups[0];
        Assert.False(string.IsNullOrWhiteSpace(firstGroup.ProcessName));
        Assert.True(firstGroup.ProcessCount >= 1);
        Assert.NotEmpty(firstGroup.SubProcesses);
        Assert.True(firstGroup.TotalMemoryBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(firstGroup.TotalMemoryDisplay));
        Assert.True(firstGroup.MainPid > 0);
    }
}
