using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Información de un esquema de energía de Windows.
/// </summary>
public sealed record PowerPlanInfo
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifiers should not contain type names")]
    public required string Guid { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public bool IsBuiltIn { get; init; }
    public PowerSchemeType SchemeType { get; init; }
}
