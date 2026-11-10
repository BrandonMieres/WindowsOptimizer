using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Servicio que detecta el tipo de medio físico de las unidades del sistema.
/// Permite adaptar las recomendaciones para SSD vs HDD.
/// </summary>
public interface IDriveMediaService
{
    /// <summary>
    /// Detecta el tipo de medio del disco del sistema operativo (unidad C: o equivalente).
    /// </summary>
    Task<DriveMediaType> GetSystemDriveMediaTypeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve información de todas las unidades físicas detectadas en el sistema.
    /// </summary>
    Task<IReadOnlyList<PhysicalDriveInfo>> GetAllDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el disco del sistema es de estado sólido (SSD o NVMe).
    /// Shortcut conveniente para los scanners.
    /// </summary>
    Task<bool> IsSystemDriveSolidStateAsync(CancellationToken cancellationToken = default);
}
