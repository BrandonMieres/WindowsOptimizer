namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Resumen global del estado de salud de dispositivos y controladores.
/// </summary>
public sealed record DeviceHealthSummary
{
    /// <summary>Total de dispositivos escaneados en el árbol de hardware.</summary>
    public int TotalDevicesCount { get; init; }

    /// <summary>Cantidad de dispositivos con algún código de error en Windows.</summary>
    public int ProblemDevicesCount { get; init; }

    /// <summary>Cantidad de controladores antiguos (> 4 años).</summary>
    public int OldDriversCount { get; init; }

    /// <summary>Cantidad de dispositivos funcionando con drivers genéricos básicos.</summary>
    public int GenericDriversCount { get; init; }

    /// <summary>Cantidad de controladores sin firma digital.</summary>
    public int UnsignedDriversCount { get; init; }

    /// <summary>Indica si el hardware del equipo está en estado óptimo sin errores.</summary>
    public bool IsHealthy => ProblemDevicesCount == 0;

    /// <summary>Descripción resumida del estado para el usuario.</summary>
    public string SummaryDescription { get; init; } = string.Empty;
}
