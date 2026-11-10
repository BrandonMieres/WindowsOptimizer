using System.Diagnostics;
using WindowsOptimizer.Core.Abstractions;

namespace WindowsOptimizer.Platform;

public sealed class SystemRestoreService : ISystemRestoreService
{
    public async Task<bool> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    // Requerido para elevar privilegios si la app principal no los tiene,
                    // pero asumimos que la app se ejecuta como admin o PowerShell pedirá UAC si no UseShellExecute=true + Verb="runas"
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
