using System.Diagnostics;
using System.Text.Json;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

public sealed class CommandFixAction : IFixAction
{
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public CommandFixAction(RuleDefinition rule)
    {
        _rule = rule;
    }

    public async Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var steps = _rule.Apply.Where(s => s.Type.Equals("command", StringComparison.OrdinalIgnoreCase)).ToList();

        string lastCommand = "Unknown";
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Command)) continue;

            lastCommand = $"{step.Command} {step.Arguments}";
            var processInfo = new ProcessStartInfo
            {
                FileName = step.Command,
                Arguments = step.Arguments ?? string.Empty,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }

        return new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "command",
            Target = "Command Line",
            AppliedValue = lastCommand,
            AppliedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        var steps = _rule.Apply.Where(s => s.Type.Equals("command", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var step in steps)
        {
            if (step.Undo is JsonElement undoElement && undoElement.ValueKind == JsonValueKind.String)
            {
                var undoCmdString = undoElement.GetString();
                if (string.IsNullOrWhiteSpace(undoCmdString)) continue;

                var parts = undoCmdString.Split(' ', 2);
                var fileName = parts[0];
                var args = parts.Length > 1 ? parts[1] : string.Empty;

                var processInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
        }
    }
}
