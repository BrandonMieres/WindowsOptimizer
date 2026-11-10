using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowsOptimizer.Scanners;
using Xunit;

namespace WindowsOptimizer.Scanners.Tests;

public class DiskAnalyzerServiceTests : IDisposable
{
    private readonly string _testRoot;

    public DiskAnalyzerServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "WinOptDiskAnalyzerTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ScanDirectoryAsync_IncludesLooseFiles_AndCalculatesAccurateTotalSizes()
    {
        // Arrange: Create a subfolder with a 2 KB file and two loose files in root (10 KB and 5 KB)
        string subDir = Path.Combine(_testRoot, "SubFolder");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "subfile.bin"), new byte[2048]);

        string bigFile = Path.Combine(_testRoot, "big_loose_file.tmp");
        File.WriteAllBytes(bigFile, new byte[10240]);

        string smallFile = Path.Combine(_testRoot, "small_loose_file.tmp");
        File.WriteAllBytes(smallFile, new byte[5120]);

        var service = new DiskAnalyzerService();

        // Act
        var result = await service.ScanDirectoryAsync(_testRoot);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsDirectory);

        // Total expected: 2048 + 10240 + 5120 = 17408 bytes
        Assert.Equal(17408, result.SizeBytes);

        // Children should contain the 10KB file, the 5KB file, and the SubFolder (2KB)
        Assert.Equal(3, result.Children.Count);

        // Children should be sorted by SizeBytes descending
        Assert.Equal("big_loose_file.tmp", result.Children[0].Name);
        Assert.False(result.Children[0].IsDirectory);
        Assert.Equal(10240, result.Children[0].SizeBytes);
        Assert.Equal("\uE8A5", result.Children[0].Glyph);

        Assert.Equal("small_loose_file.tmp", result.Children[1].Name);
        Assert.False(result.Children[1].IsDirectory);
        Assert.Equal(5120, result.Children[1].SizeBytes);

        Assert.Equal("SubFolder", result.Children[2].Name);
        Assert.True(result.Children[2].IsDirectory);
        Assert.Equal(2048, result.Children[2].SizeBytes);
        Assert.Equal("\uE8B7", result.Children[2].Glyph);
    }
}
