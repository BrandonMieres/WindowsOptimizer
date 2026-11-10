using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Rules;

/// <summary>
/// Implementación de IRuleProvider que lee archivos JSON desde el directorio de reglas.
/// </summary>
public sealed class JsonRuleProvider : IRuleProvider, IDisposable
{
    private readonly string _rulesDirectory;
    private IReadOnlyList<RuleDefinition>? _cachedRules;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonRuleProvider(string rulesDirectory)
    {
        _rulesDirectory = rulesDirectory;
    }

    public async Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedRules is not null)
        {
            return _cachedRules;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedRules is not null)
            {
                return _cachedRules;
            }

            _cachedRules = await LoadRulesFromDiskAsync(cancellationToken);
            return _cachedRules;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<RuleDefinition>> GetRulesByCategoryAsync(Category category, CancellationToken cancellationToken = default)
    {
        var allRules = await GetRulesAsync(cancellationToken);
        return allRules.Where(r => r.Category == category).ToList();
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cachedRules = await LoadRulesFromDiskAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<RuleDefinition>> LoadRulesFromDiskAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rulesDirectory))
        {
            return Array.Empty<RuleDefinition>();
        }

        var jsonFiles = Directory.GetFiles(_rulesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).Equals("presets.json", StringComparison.OrdinalIgnoreCase) &&
                        !Path.GetFileName(f).Equals("rule.schema.json", StringComparison.OrdinalIgnoreCase));

        var allRules = new List<RuleDefinition>();

        foreach (var file in jsonFiles)
        {
            try
            {
                using var stream = File.OpenRead(file);
                var rules = await JsonSerializer.DeserializeAsync<List<RuleDefinition>>(stream, _jsonOptions, cancellationToken);
                if (rules is not null)
                {
                    allRules.AddRange(rules);
                }
            }
            catch (Exception)
            {
                // TODO: Log warning that a rule file failed to parse
            }
        }

        return allRules.AsReadOnly();
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
