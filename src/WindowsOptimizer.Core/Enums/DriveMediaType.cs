namespace WindowsOptimizer.Core.Enums;

/// <summary>
/// Tipo de medio físico de la unidad de disco.
/// </summary>
public enum DriveMediaType
{
    /// <summary>Tipo desconocido o no detectado.</summary>
    Unknown = 0,

    /// <summary>Disco duro mecánico (HDD).</summary>
    Hdd = 3,

    /// <summary>Unidad de estado sólido (SSD / SATA SSD).</summary>
    Ssd = 4,

    /// <summary>Unidad NVMe / SCM (más rápido que SSD SATA).</summary>
    Nvme = 5
}
