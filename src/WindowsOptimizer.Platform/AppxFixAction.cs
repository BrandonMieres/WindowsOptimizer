using System.Diagnostics;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

public sealed class AppxFixAction : IFixAction
{
    private readonly IAppxService _appxService;
    private readonly RuleDefinition _rule;

    public string RuleId => _rule.Id;
    public bool RequiresAdmin => _rule.RequiresAdmin;

    public AppxFixAction(IAppxService appxService, RuleDefinition rule)
    {
        _appxService = appxService;
        _rule = rule;
    }

    public async Task<ChangeRecord> ApplyAsync(CancellationToken cancellationToken = default)
    {
        string targetPackagePattern = _rule.Detect?.Appx?.Name 
            ?? _rule.Apply.FirstOrDefault(s => s.Type.Equals("appx_remove", StringComparison.OrdinalIgnoreCase))?.PackageName
            ?? string.Empty;

        var apps = await _appxService.GetInstalledAppsAsync();
        var matchingApp = apps.FirstOrDefault(a => 
            a.Name.Contains(targetPackagePattern, StringComparison.OrdinalIgnoreCase) ||
            a.PackageFullName.Contains(targetPackagePattern, StringComparison.OrdinalIgnoreCase));

        string removedTarget = matchingApp?.PackageFullName ?? targetPackagePattern;

        if (matchingApp != null)
        {
            await _appxService.RemoveAppAsync(matchingApp.PackageFullName);
        }

        // Intento de desaprovisionamiento y limpieza UWP (PowerShell) para evitar que Windows lo reinstale en nuevos usuarios o actualizaciones
        try
        {
            string command = matchingApp != null
                ? $"Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*{targetPackagePattern}*' | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue"
                : $"Get-AppxPackage -Name '*{targetPackagePattern}*' | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*{targetPackagePattern}*' | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error eliminando paquete aprovisionado {targetPackagePattern}: {ex.Message}");
        }

        return new ChangeRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RuleId = _rule.Id,
            Name = _rule.Name,
            ActionType = "appx_remove",
            Target = removedTarget,
            AppliedValue = "Removed",
            AppliedAt = DateTimeOffset.UtcNow
        };
    }

    public Task RevertAsync(ChangeRecord record, CancellationToken cancellationToken = default)
    {
        // La desinstalación de aplicaciones UWP se marca como irreversible (reversible: false).
        // Las aplicaciones pueden reinstalarse desde Microsoft Store si el usuario lo desea.
        return Task.CompletedTask;
    }
}
