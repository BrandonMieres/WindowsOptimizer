using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Servicio de diagnóstico en tiempo real para analizar picos de CPU, RAM y disco.
/// </summary>
public interface ILiveDiagnosticService
{
    /// <summary>
    /// Captura una instantánea de recursos identificando los procesos con mayor consumo y posibles cuellos de botella.
    /// </summary>
    Task<LiveResourceSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Intenta finalizar de forma segura un proceso no crítico.
    /// </summary>
    Task<bool> TerminateProcessAsync(int processId, CancellationToken cancellationToken = default);
}
