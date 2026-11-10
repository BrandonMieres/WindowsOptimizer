using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

public interface IDiskAnalyzerService
{
    /// <summary>
    /// Escanea las carpetas principales (Descargas, Temp, etc.) rápidamente.
    /// </summary>
    Task<DiskNode> ScanCommonJunkFoldersAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escanea un directorio específico de forma recursiva.
    /// </summary>
    Task<DiskNode> ScanDirectoryAsync(string rootPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
