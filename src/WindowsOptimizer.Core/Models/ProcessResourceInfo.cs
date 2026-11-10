namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Métricas de consumo de recursos de un proceso en tiempo real.
/// </summary>
public sealed record ProcessResourceInfo
{
    /// <summary>Identificador del proceso (PID).</summary>
    public required int Pid { get; init; }

    /// <summary>Nombre del ejecutable (ej: 'chrome.exe', 'SearchIndexer.exe').</summary>
    public required string ProcessName { get; init; }

    /// <summary>Título de la ventana principal si tiene interfaz visible.</summary>
    public string? WindowTitle { get; init; }

    /// <summary>Porcentaje estimado de uso de CPU (0 - 100%).</summary>
    public double CpuPercentage { get; init; }

    /// <summary>Memoria RAM física en bytes (Working Set).</summary>
    public long MemoryBytes { get; init; }

    /// <summary>Representación legible de la memoria (ej: '1.2 GB', '450 MB').</summary>
    public string MemoryDisplay { get; init; } = "0 MB";

    /// <summary>Bytes de lectura/escritura en disco por segundo estimados.</summary>
    public long DiskIoBytesPerSec { get; init; }

    /// <summary>Representación legible de la actividad de disco (ej: '15.4 MB/s').</summary>
    public string DiskIoDisplay { get; init; } = "0 KB/s";

    /// <summary>Indica si es un proceso crítico del sistema operativo Windows que no debe finalizarse.</summary>
    public bool IsSystemProcess { get; init; }

    /// <summary>Indica si es un culpable común conocido de ralentizaciones (Windows Update, SearchIndexer, MsMpEng, etc.).</summary>
    public bool IsKnownCulprit { get; init; }

    /// <summary>Explicación amigable de qué hace este proceso y por qué puede estar consumiendo recursos.</summary>
    public string DiagnosisNote { get; init; } = string.Empty;

    /// <summary>Indica si el usuario puede finalizar este proceso de forma segura.</summary>
    public bool IsTerminable => !IsSystemProcess;
}
