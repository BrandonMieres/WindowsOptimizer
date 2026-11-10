using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowsOptimizer.Core.Models;

public sealed partial class DiskNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    private bool _isDirectory;
    public bool IsDirectory
    {
        get => _isDirectory;
        set
        {
            if (SetProperty(ref _isDirectory, value))
            {
                OnPropertyChanged(nameof(Glyph));
            }
        }
    }

    public string Glyph
    {
        get
        {
            if (IsDirectory) return "\uE8B7"; // Carpeta
            if (Name.StartsWith('[')) return "\uE8C8"; // Múltiples archivos / Colección
            return "\uE8A5"; // Archivo individual
        }
    }

    private long _sizeBytes;
    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(FormattedSize));
            }
        }
    }

    private double _percentageOfParent;
    public double PercentageOfParent
    {
        get => _percentageOfParent;
        set => SetProperty(ref _percentageOfParent, double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Clamp(value, 0, 1));
    }

    public ObservableCollection<DiskNode> Children { get; } = new();

    // UI Helpers
    public string FormattedSize => FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
