using System.Text.Json;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Almacén de cambios basado en archivo JSON local.
/// Guarda el estado previo de cada cambio para permitir restauración completa.
/// </summary>
public sealed class JsonRollbackStore : IRollbackStore, IDisposable
{
    private readonly string _storePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<ChangeRecord>? _cache;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonRollbackStore(string storePath)
    {
        _storePath = storePath;
        var dir = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async Task SaveAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadInternalAsync(cancellationToken);
            records.Add(record);
            await PersistAsync(records, cancellationToken);
            _cache = records;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetActiveChangesAsync(CancellationToken cancellationToken = default)
    {
        var all = await LoadSafeAsync(cancellationToken);
        return all.Where(r => !r.IsReverted).OrderByDescending(r => r.AppliedAt).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ChangeRecord>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var all = await LoadSafeAsync(cancellationToken);
        return all.OrderByDescending(r => r.AppliedAt).ToList().AsReadOnly();
    }

    public async Task<ChangeRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var all = await LoadSafeAsync(cancellationToken);
        return all.FirstOrDefault(r => r.Id == id);
    }

    public async Task MarkRevertedAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadInternalAsync(cancellationToken);
            var record = records.FirstOrDefault(r => r.Id == id);

            if (record is not null)
            {
                record.RevertedAt = DateTimeOffset.UtcNow;
            }

            await PersistAsync(records, cancellationToken);
            _cache = records;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ChangeRecord>> LoadSafeAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await LoadInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ChangeRecord>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(_storePath))
        {
            _cache = new List<ChangeRecord>();
            return _cache;
        }

        using var stream = File.OpenRead(_storePath);
        _cache = await JsonSerializer.DeserializeAsync<List<ChangeRecord>>(stream, _jsonOptions, cancellationToken)
                 ?? new List<ChangeRecord>();
        return _cache;
    }

    private async Task PersistAsync(List<ChangeRecord> records, CancellationToken cancellationToken)
    {
        using var stream = File.Create(_storePath);
        await JsonSerializer.SerializeAsync(stream, records, _jsonOptions, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var records = new List<ChangeRecord>();
            await PersistAsync(records, cancellationToken);
            _cache = records;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
