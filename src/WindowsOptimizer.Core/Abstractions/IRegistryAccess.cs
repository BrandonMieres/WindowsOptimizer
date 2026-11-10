namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Acceso al registro de Windows detrás de una interfaz para testabilidad.
/// </summary>
public interface IRegistryAccess
{
    /// <summary>Lee un valor del registro.</summary>
    object? GetValue(string keyPath, string valueName);

    /// <summary>Establece un valor en el registro.</summary>
    void SetValue(string keyPath, string valueName, object value, string kind);

    /// <summary>Elimina un valor del registro.</summary>
    void DeleteValue(string keyPath, string valueName);

    /// <summary>Comprueba si un valor existe.</summary>
    bool ValueExists(string keyPath, string valueName);

    /// <summary>Comprueba si una clave existe.</summary>
    bool KeyExists(string keyPath);
}
