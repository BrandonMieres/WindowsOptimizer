using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

/// <summary>
/// Proveedor que lee y proporciona las definiciones de reglas desde los JSON.
/// </summary>
public interface IRuleProvider
{
    /// <summary>Obtiene todas las reglas cargadas.</summary>
    Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>Obtiene las reglas de una categoría específica.</summary>
    Task<IReadOnlyList<RuleDefinition>> GetRulesByCategoryAsync(Enums.Category category, CancellationToken cancellationToken = default);

    /// <summary>Recarga las reglas desde el disco.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
