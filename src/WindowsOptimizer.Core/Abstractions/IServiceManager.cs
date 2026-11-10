using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Abstracción para gestionar servicios de Windows.
/// </summary>
public interface IServiceManager
{
    /// <summary>Obtiene el estado actual de un servicio.</summary>
    ServiceInfo? GetServiceInfo(string serviceName);

    /// <summary>Cambia el tipo de inicio de un servicio.</summary>
    void SetStartupType(string serviceName, ServiceStartMode mode);

    /// <summary>Detiene un servicio.</summary>
    void StopService(string serviceName);

    /// <summary>Inicia un servicio.</summary>
    void StartService(string serviceName);

    /// <summary>Comprueba si un servicio existe.</summary>
    bool ServiceExists(string serviceName);
}

/// <summary>
/// Información básica de un servicio de Windows.
/// </summary>
public sealed record ServiceInfo
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required ServiceStartMode StartType { get; init; }
    public required ServiceCurrentStatus Status { get; init; }
    public string? Description { get; init; }
}

public enum ServiceStartMode
{
    Automatic,
    Manual,
    Disabled,
    Boot,
    System
}

public enum ServiceCurrentStatus
{
    Running,
    Stopped,
    StartPending,
    StopPending,
    Unknown
}
