namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Representa un dispositivo de hardware con un problema o código de error reportado por Windows.
/// </summary>
public sealed record DeviceProblem
{
    /// <summary>Identificador del dispositivo en el bus PnP (ej: PCI\VEN_10DE&amp;DEV_...)</summary>
    public required string DeviceId { get; init; }

    /// <summary>Nombre del dispositivo (ej: 'NVIDIA GeForce RTX 4070' o 'Dispositivo desconocido').</summary>
    public required string Name { get; init; }

    /// <summary>Clase de hardware (ej: 'Display', 'Net', 'Media', 'Ports').</summary>
    public string ClassName { get; init; } = "Desconocido";

    /// <summary>Fabricante reportado por el hardware.</summary>
    public string Manufacturer { get; init; } = "Desconocido";

    /// <summary>Código de error de ConfigManager (ej: 10, 22, 28, 43).</summary>
    public required uint ErrorCode { get; init; }

    /// <summary>Explicación en lenguaje claro de qué significa el código de error.</summary>
    public required string ErrorDescription { get; init; }

    /// <summary>Recomendación o acción sugerida para solucionar el fallo.</summary>
    public required string Suggestion { get; init; }
}
