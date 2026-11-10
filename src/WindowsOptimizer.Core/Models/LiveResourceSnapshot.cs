namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Instantánea en tiempo real del uso de recursos del sistema para responder a '¿Por qué va lento ahora?'.
/// </summary>
public sealed record LiveResourceSnapshot
{
    /// <summary>Porcentaje global de uso de CPU del equipo (0 - 100%).</summary>
    public double TotalCpuPercentage { get; init; }

    /// <summary>Porcentaje global de uso de memoria RAM (0 - 100%).</summary>
    public double TotalRamPercentage { get; init; }

    /// <summary>Memoria RAM utilizada actualmente en bytes.</summary>
    public ulong TotalRamUsedBytes { get; init; }

    /// <summary>Memoria RAM física total del equipo en bytes.</summary>
    public ulong TotalRamBytes { get; init; }

    /// <summary>Procesos individuales con mayor uso de CPU en la instantánea.</summary>
    public IReadOnlyList<ProcessResourceInfo> TopCpuProcesses { get; init; } = [];

    /// <summary>Procesos individuales con mayor uso de memoria RAM en la instantánea.</summary>
    public IReadOnlyList<ProcessResourceInfo> TopMemoryProcesses { get; init; } = [];

    /// <summary>Procesos individuales con mayor actividad de disco en la instantánea.</summary>
    public IReadOnlyList<ProcessResourceInfo> TopDiskProcesses { get; init; } = [];

    /// <summary>Grupos de aplicaciones con mayor uso de CPU (agregados como el Administrador de Tareas).</summary>
    public IReadOnlyList<AppProcessGroup> TopCpuGroups { get; init; } = [];

    /// <summary>Grupos de aplicaciones con mayor uso de memoria RAM (agregados como el Administrador de Tareas).</summary>
    public IReadOnlyList<AppProcessGroup> TopMemoryGroups { get; init; } = [];

    /// <summary>Grupos de aplicaciones con mayor actividad de disco (agregados como el Administrador de Tareas).</summary>
    public IReadOnlyList<AppProcessGroup> TopDiskGroups { get; init; } = [];

    /// <summary>Lista de cuellos de botella o anomalías detectadas en lenguaje claro.</summary>
    public IReadOnlyList<string> Bottlenecks { get; init; } = [];

    /// <summary>Indica si el sistema tiene algún recurso saturado en este momento.</summary>
    public bool HasBottlenecks => Bottlenecks.Count > 0;

    /// <summary>Fecha y hora en que se capturó la instantánea.</summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
