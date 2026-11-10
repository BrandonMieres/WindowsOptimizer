using System.Runtime.Versioning;
using Microsoft.Win32;
using WindowsOptimizer.Core.Abstractions;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación real del acceso al registro para Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryAccess : IRegistryAccess
{
    public object? GetValue(string keyPath, string valueName)
    {
        var (hive, subKey) = ParseKeyPath(keyPath);
        using var key = hive.OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName);
    }

    public void SetValue(string keyPath, string valueName, object value, string kind)
    {
        var (hive, subKey) = ParseKeyPath(keyPath);
        using var key = hive.CreateSubKey(subKey, writable: true);
        
        var registryValueKind = ParseValueKind(kind);
        key.SetValue(valueName, value, registryValueKind);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        var (hive, subKey) = ParseKeyPath(keyPath);
        using var key = hive.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public bool ValueExists(string keyPath, string valueName)
    {
        var (hive, subKey) = ParseKeyPath(keyPath);
        using var key = hive.OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName) is not null;
    }

    public bool KeyExists(string keyPath)
    {
        var (hive, subKey) = ParseKeyPath(keyPath);
        using var key = hive.OpenSubKey(subKey, writable: false);
        return key is not null;
    }

    private static (RegistryKey Hive, string SubKey) ParseKeyPath(string keyPath)
    {
        var parts = keyPath.Split('\\', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"La ruta del registro '{keyPath}' no es válida.");
        }

        var hiveStr = parts[0].ToUpperInvariant();
        var subKey = parts[1];

        var hive = hiveStr switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Hive '{hiveStr}' no soportado.")
        };

        return (hive, subKey);
    }

    private static RegistryValueKind ParseValueKind(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "dword" => RegistryValueKind.DWord,
            "qword" => RegistryValueKind.QWord,
            "string" => RegistryValueKind.String,
            "expandstring" => RegistryValueKind.ExpandString,
            "multistring" => RegistryValueKind.MultiString,
            "binary" => RegistryValueKind.Binary,
            _ => RegistryValueKind.Unknown
        };
    }
}
