using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsOptimizer.Core.Models;

/// <summary>
/// Grupo de procesos pertenecientes a la misma aplicación (ej: múltiples procesos de navegador o subprocesos de una app).
/// Al igual que el Administrador de Tareas de Windows, agrega métricas de CPU, RAM y disco de todos sus subprocesos.
/// </summary>
public sealed class AppProcessGroup : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>Nombre del ejecutable o identificador base (ej: 'zen', 'Discord', 'chrome').</summary>
    public required string ProcessName { get; init; }

    /// <summary>Nombre legible para la interfaz.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Título principal de la ventana si alguna de las instancias tiene interfaz visible.</summary>
    public string? MainWindowTitle { get; init; }

    /// <summary>Cantidad total de procesos/instancias agrupadas en esta aplicación.</summary>
    public int ProcessCount => SubProcesses.Count;

    /// <summary>Indica si la aplicación tiene más de un proceso en ejecución para mostrar el desplegable.</summary>
    public bool HasMultipleProcesses => SubProcesses.Count > 1;

    /// <summary>PID de la instancia principal.</summary>
    public int MainPid => SubProcesses.Count > 0 ? SubProcesses[0].Pid : 0;

    /// <summary>Porcentaje agregado de uso de CPU (0 - 100%).</summary>
    public double TotalCpuPercentage { get; init; }

    /// <summary>Memoria RAM física total agregada en bytes.</summary>
    public long TotalMemoryBytes { get; init; }

    /// <summary>Representación legible de la memoria total (ej: '2.8 GB', '750 MB').</summary>
    public string TotalMemoryDisplay { get; init; } = "0 MB";

    /// <summary>Bytes agregados de lectura/escritura en disco por segundo.</summary>
    public long TotalDiskIoBytesPerSec { get; init; }

    /// <summary>Representación legible de la actividad de disco (ej: '15.4 MB/s').</summary>
    public string TotalDiskIoDisplay { get; init; } = "0 KB/s";

    /// <summary>Indica si alguno de los procesos es un proceso crítico protegido del sistema.</summary>
    public bool IsSystemProcess { get; init; }

    /// <summary>Indica si es un culpable conocido de ralentizaciones.</summary>
    public bool IsKnownCulprit { get; init; }

    /// <summary>Explicación amigable de diagnóstico.</summary>
    public string DiagnosisNote { get; init; } = string.Empty;

    /// <summary>Indica si el usuario puede forzar la finalización de la aplicación.</summary>
    public bool IsTerminable => !IsSystemProcess;

    /// <summary>Lista de subprocesos individuales con sus métricas específicas.</summary>
    public IReadOnlyList<ProcessResourceInfo> SubProcesses { get; init; } = [];

    private bool _isExpanded;

    /// <summary>Controla si el desplegable está abierto mostrando los subprocesos individuales.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandGlyph));
            }
        }
    }

    /// <summary>Icono de flecha: chevron abajo (&#xE70D;) o chevron arriba (&#xE70E;).</summary>
    public string ExpandGlyph => _isExpanded ? "\uE70E" : "\uE70D";
}
