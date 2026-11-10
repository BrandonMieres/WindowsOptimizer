using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Servicio de auditoría de hardware, salud de dispositivos y controladores instalados.
/// </summary>
public interface IDeviceHealthService
{
    /// <summary>
    /// Escanea el árbol de dispositivos Plug and Play y controladores firmados en Windows.
    /// </summary>
    Task<(IReadOnlyList<DeviceProblem> Problems, IReadOnlyList<DriverItem> Drivers, DeviceHealthSummary Summary)> AuditDevicesAndDriversAsync(CancellationToken cancellationToken = default);
}
