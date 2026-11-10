using System.IO;
using System.Linq;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Rules;
using Xunit;

namespace WindowsOptimizer.Tests;

public class RulesValidationTests
{
    private readonly string _rulesDirectory;

    public RulesValidationTests()
    {
        // Calculate the path to the rules directory from the test execution folder
        // Typically: src/WindowsOptimizer.Tests/bin/Debug/net10.0/
        // We want to reach the root /rules/ directory.
        _rulesDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "rules"));
    }

    [Fact]
    public void RulesDirectory_Exists()
    {
        Assert.True(Directory.Exists(_rulesDirectory), $"El directorio de reglas no se encontró en: {_rulesDirectory}");
    }

    [Fact]
    public async Task JsonRuleProvider_CanLoadAllRules_WithoutExceptions()
    {
        var provider = new JsonRuleProvider(_rulesDirectory);
        var rules = await provider.GetRulesAsync();
        
        Assert.NotEmpty(rules);
    }

    [Fact]
    public async Task Rules_HaveUniqueIds()
    {
        var provider = new JsonRuleProvider(_rulesDirectory);
        var rules = await provider.GetRulesAsync();

        var duplicateIds = rules.GroupBy(r => r.Id)
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToList();

        Assert.Empty(duplicateIds);
    }

    [Fact]
    public async Task Rules_HaveValidCategories()
    {
        var provider = new JsonRuleProvider(_rulesDirectory);
        var rules = await provider.GetRulesAsync();

        foreach (var rule in rules)
        {
            Assert.True(Enum.IsDefined(rule.Category), $"Regla {rule.Id} tiene una categoría inválida: {rule.Category}");
        }
    }

    [Fact]
    public async Task ReversibleRules_HaveUndoInformation()
    {
        // En nuestro diseño, undo information puede ser manejado por JsonRollbackStore y los snapshots automáticos.
        // Pero las reglas deben tener Reversible = true/false explícito.
        // Si Reversible es falso, debe tener riesgo alto o una justificación.
        var provider = new JsonRuleProvider(_rulesDirectory);
        var rules = await provider.GetRulesAsync();

        foreach (var rule in rules)
        {
            if (!rule.Reversible)
            {
                // Reglas irreversibles como Cleaners son correctas, pero en Privacy u otras deben estar bien documentadas.
                Assert.NotNull(rule.Explain);
                Assert.NotEmpty(rule.Explain.What);
            }
        }
    }

    [Fact]
    public async Task Rules_SpecifyPresets()
    {
        // Al menos debe tener un preset asignado para poder aparecer en algún lado
        var provider = new JsonRuleProvider(_rulesDirectory);
        var rules = await provider.GetRulesAsync();

        foreach (var rule in rules)
        {
            Assert.True(rule.Presets != null && rule.Presets.Count > 0, $"Regla {rule.Id} no tiene ningún preset especificado.");
        }
    }
}
