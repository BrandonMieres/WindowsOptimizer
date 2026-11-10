using System.Runtime.Versioning;
using System.Text.Json;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

[SupportedOSPlatform("windows")]
public sealed class CleanerFixAction : IFixAction
{
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public CleanerFixAction(RuleDefinition rule)
    {
        _rule = rule;
    }

    private static (long bytesFreed, int lockedCount) CleanDirectory(string path)
    {
        long bytesFreed = 0;
        int lockedCount = 0;
        
        var di = new DirectoryInfo(path);
        if (!di.Exists) return (0, 0);

        foreach (var file in di.GetFiles())
        {
            try
            {
                long size = file.Length;
                file.Delete();
                bytesFreed += size;
            }
            catch { lockedCount++; }
        }

        foreach (var dir in di.GetDirectories())
        {
            var res = CleanDirectory(dir.FullName);
            bytesFreed += res.bytesFreed;
            lockedCount += res.lockedCount;

            try { dir.Delete(false); } catch { lockedCount++; }
        }

        return (bytesFreed, lockedCount);
    }

    public Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var steps = _rule.Apply.Where(s => s.Type.Equals("folder_clean", StringComparison.OrdinalIgnoreCase)).ToList();

        long bytesFreed = 0;
        int lockedCount = 0;
        foreach (var step in steps)
        {
            if (string.IsNullOrEmpty(step.Path)) continue;

            var expandedPaths = WindowsOptimizer.Core.Utilities.VariableExpander.ExpandPaths(step.Path);
            foreach (var expandedPath in expandedPaths)
            {
                if (Directory.Exists(expandedPath))
                {
                    var res = CleanDirectory(expandedPath);
                    bytesFreed += res.bytesFreed;
                    lockedCount += res.lockedCount;
                }
                else if (File.Exists(expandedPath))
                {
                    try
                    {
                        var fi = new FileInfo(expandedPath);
                        long size = fi.Length;
                        fi.Delete();
                        bytesFreed += size;
                    }
                    catch { lockedCount++; }
                }
            }
        }

        string msg = $"{bytesFreed / 1048576} MB liberados";
        if (lockedCount > 0)
        {
            msg += $" ({lockedCount} bloqueados)";
        }

        var record = new ChangeRecord
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "folder_clean",
            Target = "Folders",
            AppliedValue = msg,
            AppliedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(record);
    }

    public Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        // Cleaning is irreversible
        return Task.CompletedTask;
    }
}
