namespace WindowsOptimizer.Core.Models;

public sealed class AppxPackage
{
    public string Name { get; init; } = string.Empty;
    public string PackageFullName { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsFramework { get; init; }
    public bool IsSystem { get; init; }
    public bool IsSelected { get; set; }
}
