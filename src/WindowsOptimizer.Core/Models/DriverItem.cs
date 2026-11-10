namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Información sobre un controlador instalado en el sistema.
/// </summary>
public sealed record DriverItem
{
    /// <summary>Nombre del dispositivo asociado al controlador.</summary>
    public required string DeviceName { get; init; }

    /// <summary>Clase de dispositivo (ej: 'DISPLAY', 'NET', 'MEDIA', 'STORAGE').</summary>
    public string DeviceClass { get; init; } = "Sistema";

    /// <summary>Proveedor o fabricante del controlador (ej: 'NVIDIA', 'Intel', 'Microsoft').</summary>
    public string DriverProvider { get; init; } = "Desconocido";

    /// <summary>Versión del controlador instalada.</summary>
    public string DriverVersion { get; init; } = "Desconocida";

    /// <summary>Fecha de publicación del controlador.</summary>
    public DateTimeOffset? DriverDate { get; init; }

    /// <summary>Indica si el controlador cuenta con firma digital válida (WHQL).</summary>
    public bool IsSigned { get; init; } = true;

    /// <summary>Indica si el driver es un controlador genérico básico de Microsoft en lugar del oficial del fabricante.</summary>
    public bool IsGenericMicrosoftDriver { get; init; }

    /// <summary>Indica si el controlador tiene más de 4 años de antigüedad.</summary>
    public bool IsOld { get; init; }

    /// <summary>Texto de estado o recomendación para la interfaz.</summary>
    public string StatusText { get; init; } = "Correcto";
}
