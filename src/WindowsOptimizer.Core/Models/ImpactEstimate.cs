using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Estimación orientativa de mejora asociada a un hallazgo.
/// Principio: solo se muestra un porcentaje cuando hay datos medidos que lo respalden.
/// </summary>
public sealed record ImpactEstimate
{
    /// <summary>Tipo de mejora esperada.</summary>
    public required ImpactType Type { get; init; }

    /// <summary>Nivel de confianza en la estimación.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>Si se muestra un porcentaje estimado al usuario.</summary>
    public required bool ShowPercentage { get; init; }

    /// <summary>Límite inferior del rango estimado (ej: 10 para "10-20 %").</summary>
    public double? MinPercentage { get; init; }

    /// <summary>Límite superior del rango estimado (ej: 20 para "10-20 %").</summary>
    public double? MaxPercentage { get; init; }

    /// <summary>Valor absoluto estimado (ej: bytes de espacio, ms de arranque).</summary>
    public long? AbsoluteValue { get; init; }

    /// <summary>Unidad del valor absoluto (ej: "bytes", "ms", "MB").</summary>
    public string? Unit { get; init; }

    /// <summary>Descripción legible de la mejora (ej: "Reduce el envío de datos").</summary>
    public string? Description { get; init; }
}
