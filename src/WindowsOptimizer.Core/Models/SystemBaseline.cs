namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Línea base del sistema medida en reposo.
/// Almacena métricas objetivas del equipo (arranque, RAM, procesos, disco).
/// </summary>
public sealed record SystemBaseline
{
    /// <summary>Duración del último arranque del sistema (EventID 100 Diagnostics-Performance o Uptime).</summary>
    public TimeSpan? BootDuration { get; init; }

    /// <summary>Fecha y hora del último inicio del sistema.</summary>
    public DateTimeOffset? LastBootTime { get; init; }

    /// <summary>Memoria RAM física total en bytes.</summary>
    public ulong TotalRamBytes { get; init; }

    /// <summary>Memoria RAM física disponible en bytes.</summary>
    public ulong AvailableRamBytes { get; init; }

    /// <summary>Memoria RAM física en uso en bytes.</summary>
    public ulong UsedRamBytes => TotalRamBytes > AvailableRamBytes ? TotalRamBytes - AvailableRamBytes : 0;

    /// <summary>Porcentaje de uso de memoria RAM actual (0-100%).</summary>
    public double RamUsagePercentage => TotalRamBytes > 0 ? (double)UsedRamBytes / TotalRamBytes * 100.0 : 0.0;

    /// <summary>Número total de procesos activos en el sistema.</summary>
    public int ProcessCount { get; init; }

    /// <summary>Tiempo total de actividad ininterrumpida del sistema.</summary>
    public TimeSpan SystemUptime { get; init; }

    /// <summary>Espacio libre en la unidad del sistema (C:) en bytes.</summary>
    public ulong SystemDriveFreeBytes { get; init; }

    /// <summary>Espacio total en la unidad del sistema (C:) en bytes.</summary>
    public ulong SystemDriveTotalBytes { get; init; }

    /// <summary>Fecha y hora en que se tomaron estas métricas.</summary>
    public DateTimeOffset MeasuredAt { get; init; } = DateTimeOffset.UtcNow;
}
