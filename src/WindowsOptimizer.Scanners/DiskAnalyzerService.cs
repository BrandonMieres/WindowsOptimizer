using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Scanners;

public sealed class DiskAnalyzerService : IDiskAnalyzerService
{
    private static readonly EnumerationOptions _enumOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false, // We'll recurse manually to build the tree
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public async Task<DiskNode> ScanCommonJunkFoldersAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var root = new DiskNode
        {
            Name = "Archivos innecesarios comunes",
            FullPath = "CommonJunk",
            IsDirectory = true
        };

        var foldersToScan = new List<string>
        {
            Path.GetTempPath(), // %TEMP%
            Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), // Temporary Internet Files
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download")
        };

        // Escanear en paralelo
        var tasks = foldersToScan.Select(path => Task.Run(async () =>
        {
            if (Directory.Exists(path))
            {
                progress?.Report($"Analizando: {path}");
                return await ScanDirectoryAsync(path, progress, cancellationToken);
            }
            return null;
        }, cancellationToken));

        var results = await Task.WhenAll(tasks);
        
        foreach (var result in results)
        {
            if (result != null)
            {
                root.Children.Add(result);
                root.SizeBytes += result.SizeBytes;
            }
        }

        // Calculate percentages
        if (root.SizeBytes > 0)
        {
            foreach (var child in root.Children)
            {
                child.PercentageOfParent = (double)child.SizeBytes / root.SizeBytes;
            }
        }

        // Ordenar de mayor a menor y mantener solo los 100 elementos más grandes
        var sortedList = root.Children.OrderByDescending(c => c.SizeBytes).Take(100).ToList();
        root.Children.Clear();
        foreach (var child in sortedList)
        {
            root.Children.Add(child);
        }

        return root;
    }

    public Task<DiskNode> ScanDirectoryAsync(string rootPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ScanIterative(rootPath, progress, cancellationToken), cancellationToken);
    }

    private static DiskNode ScanIterative(string rootPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var rootNode = new DiskNode
        {
            Name = string.IsNullOrEmpty(Path.GetFileName(rootPath)) ? rootPath : Path.GetFileName(rootPath),
            FullPath = rootPath,
            IsDirectory = true
        };

        var dirsToProcess = new Stack<(DirectoryInfo Dir, DiskNode Node, int Depth)>();
        dirsToProcess.Push((new DirectoryInfo(rootPath), rootNode, 0));

        long foldersProcessed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (dirsToProcess.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (currentDir, currentNode, currentDepth) = dirsToProcess.Pop();

            if (currentDepth > 50) continue; // Límite estricto

            FileInfo[]? files = null;
            try
            {
                files = currentDir.GetFiles();
            }
            catch (Exception) { /* Ignorar accesos denegados a archivos */ }

            if (files != null && files.Length > 0)
            {
                // Ordenar archivos por tamaño descendente para priorizar los mayores consumidores de espacio
                var nonReparseFiles = files.Where(f => !f.Attributes.HasFlag(FileAttributes.ReparsePoint)).ToList();
                nonReparseFiles.Sort((a, b) => b.Length.CompareTo(a.Length));

                const int maxIndividualFiles = 30;
                int filesToAdd = Math.Min(nonReparseFiles.Count, maxIndividualFiles);

                for (int i = 0; i < filesToAdd; i++)
                {
                    var file = nonReparseFiles[i];
                    if (file.Length <= 0 && i >= 5) break; // Evitar inundar con archivos vacíos

                    currentNode.Children.Add(new DiskNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        SizeBytes = file.Length
                    });
                }

                if (nonReparseFiles.Count > filesToAdd)
                {
                    long remainingBytes = 0;
                    int remainingCount = 0;
                    for (int i = filesToAdd; i < nonReparseFiles.Count; i++)
                    {
                        remainingBytes += nonReparseFiles[i].Length;
                        remainingCount++;
                    }

                    if (remainingBytes > 0)
                    {
                        currentNode.Children.Add(new DiskNode
                        {
                            Name = $"[Otros archivos sueltos ({remainingCount})]",
                            FullPath = Path.Combine(currentDir.FullName, "*.*"),
                            IsDirectory = false,
                            SizeBytes = remainingBytes
                        });
                    }
                }
            }

            try
            {
                var subDirs = currentDir.GetDirectories();
                foreach (var subDir in subDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;

                    var childNode = new DiskNode
                    {
                        Name = subDir.Name,
                        FullPath = subDir.FullName,
                        IsDirectory = true
                    };
                    
                    currentNode.Children.Add(childNode);
                    dirsToProcess.Push((subDir, childNode, currentDepth + 1));
                }
            }
            catch (Exception) { /* Ignorar accesos denegados a carpetas */ }

            foldersProcessed++;
            if (foldersProcessed % 50 == 0 && sw.ElapsedMilliseconds > 100)
            {
                progress?.Report($"Analizando... {foldersProcessed} carpetas");
                sw.Restart();
            }
        }

        CalculateTotalSizes(rootNode);

        return rootNode;
    }

    private static void CalculateTotalSizes(DiskNode node)
    {
        if (node.IsDirectory)
        {
            long total = 0;
            foreach (var child in node.Children)
            {
                if (child.IsDirectory)
                {
                    CalculateTotalSizes(child);
                }
                total += child.SizeBytes;
            }
            node.SizeBytes = total;
        }

        if (node.SizeBytes > 0)
        {
            foreach (var child in node.Children)
            {
                child.PercentageOfParent = (double)child.SizeBytes / node.SizeBytes;
            }
            
            // Ordenar de mayor a menor y mantener solo los 100 elementos más grandes en memoria y en la interfaz.
            // ¡Esto resuelve el problema de "ponerlo todo de golpe" porque destruimos las miles de carpetas pequeñas irrelevantes!
            var sortedList = node.Children
                .Where(c => c.SizeBytes > 0)
                .OrderByDescending(c => c.SizeBytes)
                .Take(100)
                .ToList();
                
            node.Children.Clear();
            foreach (var child in sortedList)
            {
                node.Children.Add(child);
            }
        }
    }
}
