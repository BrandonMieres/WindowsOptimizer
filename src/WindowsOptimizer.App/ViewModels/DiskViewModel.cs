using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class DiskViewModel : ObservableObject, IDisposable
{
    private readonly IDiskAnalyzerService _diskAnalyzer;
    private readonly ScanOrchestrator _scanOrchestrator;
    private readonly FixOrchestrator _fixOrchestrator;
    private readonly IDialogService _dialogService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cancellationTokenSource;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = "Listo para escanear disco.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private DiskNode? _rootNode;
    public DiskNode? RootNode
    {
        get => _rootNode;
        set => SetProperty(ref _rootNode, value);
    }

    public ObservableCollection<DiskNode> TopFolders { get; } = new();

    public DiskViewModel(IDiskAnalyzerService diskAnalyzer, ScanOrchestrator scanOrchestrator, FixOrchestrator fixOrchestrator, IDialogService dialogService)
    {
        _diskAnalyzer = diskAnalyzer;
        _scanOrchestrator = scanOrchestrator;
        _fixOrchestrator = fixOrchestrator;
        _dialogService = dialogService;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    private async Task ScanCommonJunkAsync()
    {
        await StartScanAsync(() => _diskAnalyzer.ScanCommonJunkFoldersAsync(
            new Progress<string>(msg => _dispatcherQueue.TryEnqueue(() => StatusMessage = msg)),
            _cancellationTokenSource!.Token));
    }

    [RelayCommand]
    private async Task ScanCDriveAsync()
    {
        await StartScanAsync(() => _diskAnalyzer.ScanDirectoryAsync(
            "C:\\",
            new Progress<string>(msg => _dispatcherQueue.TryEnqueue(() => StatusMessage = msg)),
            _cancellationTokenSource!.Token));
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelando...";
    }

    [RelayCommand]
    private async Task CleanJunkAsync()
    {
        IsLoading = true;
        StatusMessage = "Analizando elementos a limpiar...";

        try
        {
            var result = await _scanOrchestrator.RunFullScanAsync();
            var cleaners = result.Findings.Where(f => f.Category == WindowsOptimizer.Core.Enums.Category.Cleaners && f.Status == WindowsOptimizer.Core.Enums.FindingStatus.Detected).ToList();

            if (cleaners.Count == 0)
            {
                await _dialogService.ShowConfirmationAsync(
                    "Liberar Espacio",
                    "No se ha encontrado ningún archivo basura o temporal para limpiar. Tu sistema ya está limpio.",
                    "Aceptar",
                    "");
                return;
            }

            string itemsList = string.Join("\n", cleaners.Select(c => $"• {c.Name} ({c.CurrentValue})"));
            
            long totalMB = 0;
            foreach (var c in cleaners)
            {
                var valStr = c.CurrentValue?.ToString()?.Replace(" MB", "");
                if (long.TryParse(valStr, out long val))
                {
                    totalMB += val;
                }
            }
            
            string message = $"Se van a limpiar las siguientes ubicaciones para liberar {totalMB} MB de espacio:\n\n{itemsList}\n\n¿Estás seguro de que deseas continuar y borrar estos archivos permanentemente?";

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Liberar Espacio",
                message,
                "Limpiar",
                "Cancelar");

            if (!confirm) return;

            StatusMessage = "Iniciando motor de limpieza...";
            int applied = 0;
            foreach (var cleaner in cleaners)
            {
                StatusMessage = $"Limpiando: {cleaner.Name}";
                try
                {
                    await _fixOrchestrator.ApplyFixAsync(cleaner);
                    applied++;
                }
                catch { }
            }

            StatusMessage = $"Limpieza completada. {applied} elementos limpiados.";
            
            // Re-scan para actualizar la vista
            await ScanCommonJunkAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al limpiar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task StartScanAsync(Func<Task<DiskNode>> scanAction)
    {
        if (IsLoading) return;

        IsLoading = true;
        TopFolders.Clear();
        RootNode = null;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            StatusMessage = "Iniciando escaneo...";
            
            var result = await Task.Run(scanAction);
            RootNode = result;

            foreach (var child in result.Children)
            {
                TopFolders.Add(child);
            }

            StatusMessage = $"Escaneo completado. Tamaño total: {result.FormattedSize}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Escaneo cancelado por el usuario.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public async Task DeleteFolderAsync(DiskNode node)
    {
        string itemType = node.IsDirectory ? "la carpeta" : "el archivo";
        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Borrar Elemento",
            $"¿Estás seguro de que deseas eliminar permanentemente {itemType} '{node.Name}' ({node.FormattedSize})?\n\nRuta: {node.FullPath}\n\nEsta acción NO se puede deshacer.",
            "Borrar Permanentemente",
            "Cancelar");

        if (!confirm) return;

        IsLoading = true;
        StatusMessage = $"Borrando {node.Name}...";

        try
        {
            long deletedBytes = 0;
            int lockedCount = 0;

            await Task.Run(() => 
            {
                if (node.IsDirectory)
                {
                    var result = ForceDeleteDirectory(node.FullPath);
                    deletedBytes = result.deletedBytes;
                    lockedCount = result.lockedCount;

                    try { System.IO.Directory.Delete(node.FullPath, false); } catch { lockedCount++; }
                }
                else
                {
                    if (node.FullPath.EndsWith("*.*", StringComparison.Ordinal))
                    {
                        var dirPath = System.IO.Path.GetDirectoryName(node.FullPath);
                        if (!string.IsNullOrEmpty(dirPath) && System.IO.Directory.Exists(dirPath))
                        {
                            var dir = new System.IO.DirectoryInfo(dirPath);
                            foreach (var file in dir.GetFiles())
                            {
                                try
                                {
                                    long len = file.Length;
                                    file.Delete();
                                    deletedBytes += len;
                                }
                                catch { lockedCount++; }
                            }
                        }
                    }
                    else
                    {
                        try 
                        { 
                            var fileInfo = new System.IO.FileInfo(node.FullPath);
                            if (fileInfo.Exists)
                            {
                                deletedBytes = fileInfo.Length;
                                fileInfo.Delete(); 
                            }
                        } 
                        catch { lockedCount++; }
                    }
                }
            });

            if (lockedCount > 0)
            {
                StatusMessage = $"Proceso completado con advertencias. {lockedCount} archivo(s) en uso.";
                await _dialogService.ShowMessageAsync("Aviso", $"No se pudieron borrar {lockedCount} elemento(s) porque están en uso por el sistema o faltan permisos.\n\nEspacio liberado: {Math.Round((double)deletedBytes / 1048576, 2)} MB");
                
                // Update size without removing
                node.SizeBytes -= deletedBytes;
                if (node.SizeBytes <= 0)
                {
                    _dispatcherQueue.TryEnqueue(() => RemoveNodeFromTree(node));
                }
            }
            else
            {
                StatusMessage = $"Proceso de borrado de '{node.Name}' completado.";
                _dispatcherQueue.TryEnqueue(() => RemoveNodeFromTree(node));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al borrar '{node.Name}': {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RemoveNodeFromTree(DiskNode target)
    {
        TopFolders.Remove(target);
        if (RootNode != null)
        {
            RemoveNodeAndSubtractSize(RootNode, target);
        }
    }

    private static bool RemoveNodeAndSubtractSize(DiskNode? parent, DiskNode target)
    {
        if (parent == null) return false;

        if (parent.Children.Remove(target))
        {
            parent.SizeBytes -= target.SizeBytes;
            return true;
        }

        foreach (var child in parent.Children)
        {
            if (RemoveNodeAndSubtractSize(child, target))
            {
                parent.SizeBytes -= target.SizeBytes;
                return true;
            }
        }
        return false;
    }

    private static (long deletedBytes, int lockedCount) ForceDeleteDirectory(string path)
    {
        long deletedBytes = 0;
        int lockedCount = 0;
        try
        {
            var directory = new System.IO.DirectoryInfo(path);
            if (!directory.Exists) return (0, 0);

            foreach (var file in directory.GetFiles())
            {
                try 
                { 
                    long size = file.Length;
                    file.Delete(); 
                    deletedBytes += size;
                } 
                catch { lockedCount++; }
            }

            foreach (var subDir in directory.GetDirectories())
            {
                var result = ForceDeleteDirectory(subDir.FullName);
                deletedBytes += result.deletedBytes;
                lockedCount += result.lockedCount;
                
                try { subDir.Delete(false); } catch { lockedCount++; }
            }
        }
        catch { lockedCount++; } 
        return (deletedBytes, lockedCount);
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
    }
}
