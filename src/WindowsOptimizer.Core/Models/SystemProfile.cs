namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Información del hardware y entorno del equipo.
/// Se usa para adaptar las recomendaciones (portátil/sobremesa, SSD/HDD, etc.).
/// </summary>
public sealed class SystemProfile
{
    /// <summary>Versión de Windows (ej: "10.0.19045").</summary>
    public required string WindowsVersion { get; init; }

    /// <summary>Build de Windows (ej: 19045).</summary>
    public required int WindowsBuild { get; init; }

    /// <summary>Edición de Windows (ej: "Pro", "Home").</summary>
    public required string WindowsEdition { get; init; }

    /// <summary>Si es un portátil.</summary>
    public bool IsLaptop { get; init; }

    /// <summary>Si el disco del sistema es SSD.</summary>
    public bool HasSsd { get; init; }

    /// <summary>Nombre del procesador.</summary>
    public string? CpuName { get; init; }

    /// <summary>Número de núcleos lógicos.</summary>
    public int LogicalProcessors { get; init; }

    /// <summary>Memoria RAM total en bytes.</summary>
    public long TotalRamBytes { get; init; }

    /// <summary>Nombre de la GPU dedicada (null si no tiene).</summary>
    public string? GpuName { get; init; }

    /// <summary>Si está gestionado por empresa (dominio, MDM).</summary>
    public bool IsManagedByOrganization { get; init; }

    /// <summary>RAM total formateada para mostrar.</summary>
    public string TotalRamFormatted =>
        TotalRamBytes switch
        {
            >= 1L << 30 => $"{TotalRamBytes / (1L << 30)} GB",
            >= 1L << 20 => $"{TotalRamBytes / (1L << 20)} MB",
            _ => $"{TotalRamBytes} bytes"
        };
}
