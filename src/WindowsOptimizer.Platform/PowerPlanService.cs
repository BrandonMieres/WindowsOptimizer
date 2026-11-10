using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación de IPowerPlanService usando APIs Win32 (powrprof.dll) con fallback a powercfg.
/// </summary>
public sealed class PowerPlanService : IPowerPlanService
{
    private static readonly Regex PowerCfgListRegex = new(
        @"GUID.*?:\s*([a-f0-9\-]{36})\s*\((.*?)\)\s*(\*?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
    public const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public async Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var plans = TryGetPlansFromWin32();
            if (plans.Count > 0)
            {
                return (IReadOnlyList<PowerPlanInfo>)plans;
            }

            return GetPlansFromPowerCfg();
        }, cancellationToken);
    }

    public async Task<PowerPlanInfo?> GetActivePlanAsync(CancellationToken cancellationToken = default)
    {
        var plans = await GetPowerPlansAsync(cancellationToken);
        if (plans.Count == 0) return null;

        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].IsActive) return plans[i];
        }

        return plans[0];
    }

    public async Task<bool> SetActivePlanAsync(string planGuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planGuid) || !Guid.TryParse(planGuid, out var guid))
        {
            return false;
        }

        return await Task.Run(() =>
        {
            // Intentar primero con la API nativa de Windows (instantáneo, sin ventana, sin UAC)
            uint result = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref guid);
            if (result == 0)
            {
                return true;
            }

            // Fallback a powercfg /setactive
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo("powercfg", $"/setactive {planGuid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public async Task<PowerPlanInfo?> EnableUltimatePerformanceAsync(CancellationToken cancellationToken = default)
    {
        var plans = await GetPowerPlansAsync(cancellationToken);

        // Si ya existe un plan de Máximo Rendimiento, simplemente lo activamos para no duplicarlo infinitamente
        var existingUltimate = plans.FirstOrDefault(p =>
            p.SchemeType == PowerSchemeType.UltimatePerformance ||
            p.Guid.Equals(UltimatePerformanceTemplateGuid, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Máximo rendimiento", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Maximo rendimiento", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase));

        if (existingUltimate != null)
        {
            await SetActivePlanAsync(existingUltimate.Guid, cancellationToken);
            return existingUltimate with { IsActive = true };
        }

        // Si no existe, duplicamos la plantilla oculta de Microsoft
        return await Task.Run(async () =>
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo("powercfg", $"-duplicatescheme {UltimatePerformanceTemplateGuid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };
                p.Start();
                string output = await p.StandardOutput.ReadToEndAsync(cancellationToken);
                await p.WaitForExitAsync(cancellationToken);

                var match = PowerCfgListRegex.Match(output);
                string newGuid = match.Success ? match.Groups[1].Value : UltimatePerformanceTemplateGuid;

                await SetActivePlanAsync(newGuid, cancellationToken);

                var updatedPlans = await GetPowerPlansAsync(cancellationToken);
                return updatedPlans.FirstOrDefault(x => x.Guid.Equals(newGuid, StringComparison.OrdinalIgnoreCase))
                    ?? new PowerPlanInfo
                    {
                        Guid = newGuid,
                        Name = "Máximo rendimiento",
                        Description = "Proporciona el máximo rendimiento en equipos de alto desempeño.",
                        IsActive = true,
                        IsBuiltIn = false,
                        SchemeType = PowerSchemeType.UltimatePerformance
                    };
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    public async Task<bool> DeletePlanAsync(string planGuid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planGuid) || !Guid.TryParse(planGuid, out var guid))
        {
            return false;
        }

        // Proteger planes base del sistema
        if (IsBuiltInGuid(planGuid))
        {
            return false;
        }

        return await Task.Run(() =>
        {
            uint res = NativeMethods.PowerDeleteScheme(IntPtr.Zero, ref guid);
            if (res == 0)
            {
                return true;
            }

            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo("powercfg", $"-delete {planGuid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private static List<PowerPlanInfo> TryGetPlansFromWin32()
    {
        var result = new List<PowerPlanInfo>();
        try
        {
            string activeGuidStr = string.Empty;
            if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr pActiveGuid) == 0 && pActiveGuid != IntPtr.Zero)
            {
                try
                {
                    var activeGuid = Marshal.PtrToStructure<Guid>(pActiveGuid);
                    activeGuidStr = activeGuid.ToString();
                }
                finally
                {
                    NativeMethods.LocalFree(pActiveGuid);
                }
            }

            uint index = 0;
            while (true)
            {
                uint bufferSize = 16;
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    uint enumRes = NativeMethods.PowerEnumerate(
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        (uint)NativeMethods.PowerDataAccessor.ACCESS_SCHEME,
                        index,
                        buffer,
                        ref bufferSize);

                    if (enumRes != 0) break;

                    var schemeGuid = Marshal.PtrToStructure<Guid>(buffer);
                    string guidStr = schemeGuid.ToString();

                    string friendlyName = GetSchemeFriendlyName(schemeGuid) ?? GetDefaultSchemeName(guidStr);
                    string? description = GetSchemeDescription(schemeGuid);

                    bool isActive = !string.IsNullOrEmpty(activeGuidStr) &&
                                    guidStr.Equals(activeGuidStr, StringComparison.OrdinalIgnoreCase);

                    var schemeType = ClassifyScheme(guidStr, friendlyName);
                    bool isBuiltIn = IsBuiltInGuid(guidStr);

                    result.Add(new PowerPlanInfo
                    {
                        Guid = guidStr,
                        Name = friendlyName,
                        Description = description,
                        IsActive = isActive,
                        IsBuiltIn = isBuiltIn,
                        SchemeType = schemeType
                    });

                    index++;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch
        {
            // Fallback a powercfg
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
    private static ReadOnlyCollection<PowerPlanInfo> GetPlansFromPowerCfg()
    {
        var result = new List<PowerPlanInfo>();
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo("powercfg", "/list")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var matches = PowerCfgListRegex.Matches(output);
            foreach (Match match in matches)
            {
                var schemeGuidStr = match.Groups[1].Value.Trim();
                var name = match.Groups[2].Value.Trim();
                var isActive = match.Groups[3].Value.Contains('*');

                var schemeType = ClassifyScheme(schemeGuidStr, name);
                var isBuiltIn = IsBuiltInGuid(schemeGuidStr);

                result.Add(new PowerPlanInfo
                {
                    Guid = schemeGuidStr,
                    Name = string.IsNullOrWhiteSpace(name) ? GetDefaultSchemeName(schemeGuidStr) : name,
                    Description = GetSchemeDefaultDescription(schemeType),
                    IsActive = isActive,
                    IsBuiltIn = isBuiltIn,
                    SchemeType = schemeType
                });
            }
        }
        catch
        {
            // Silencioso
        }

        return result.AsReadOnly();
    }

    private static string? GetSchemeFriendlyName(Guid schemeGuid)
    {
        uint bufferSize = 0;
        _ = NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);
        if (bufferSize == 0) return null;

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, buffer, ref bufferSize) == 0)
            {
                return Marshal.PtrToStringUni(buffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return null;
    }

    private static string? GetSchemeDescription(Guid schemeGuid)
    {
        uint bufferSize = 0;
        _ = NativeMethods.PowerReadDescription(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);
        if (bufferSize == 0) return null;

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (NativeMethods.PowerReadDescription(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, buffer, ref bufferSize) == 0)
            {
                return Marshal.PtrToStringUni(buffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return null;
    }

    public static PowerSchemeType ClassifyScheme(string schemeGuidStr, string name)
    {
        if (schemeGuidStr.Equals(BalancedGuid, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Equilibrado", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Balanced", StringComparison.OrdinalIgnoreCase))
        {
            return PowerSchemeType.Balanced;
        }

        if (schemeGuidStr.Equals(UltimatePerformanceTemplateGuid, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Máximo rendimiento", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Maximo rendimiento", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
        {
            return PowerSchemeType.UltimatePerformance;
        }

        if (schemeGuidStr.Equals(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Alto rendimiento", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("High performance", StringComparison.OrdinalIgnoreCase))
        {
            return PowerSchemeType.HighPerformance;
        }

        if (schemeGuidStr.Equals(PowerSaverGuid, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Economizador", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Power Saver", StringComparison.OrdinalIgnoreCase))
        {
            return PowerSchemeType.PowerSaver;
        }

        return PowerSchemeType.Custom;
    }

    public static bool IsBuiltInGuid(string schemeGuidStr)
    {
        return schemeGuidStr.Equals(BalancedGuid, StringComparison.OrdinalIgnoreCase) ||
               schemeGuidStr.Equals(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase) ||
               schemeGuidStr.Equals(PowerSaverGuid, StringComparison.OrdinalIgnoreCase) ||
               schemeGuidStr.Equals(UltimatePerformanceTemplateGuid, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDefaultSchemeName(string schemeGuidStr)
    {
        return ClassifyScheme(schemeGuidStr, string.Empty) switch
        {
            PowerSchemeType.Balanced => "Equilibrado",
            PowerSchemeType.HighPerformance => "Alto rendimiento",
            PowerSchemeType.PowerSaver => "Economizador",
            PowerSchemeType.UltimatePerformance => "Máximo rendimiento",
            _ => "Plan personalizado"
        };
    }

    private static string GetSchemeDefaultDescription(PowerSchemeType schemeType)
    {
        return schemeType switch
        {
            PowerSchemeType.Balanced => "Equilibra automáticamente el rendimiento y el consumo de energía en el hardware adecuado.",
            PowerSchemeType.HighPerformance => "Favorece el rendimiento del procesador y reduce la latencia, aumentando el consumo de energía.",
            PowerSchemeType.PowerSaver => "Ahorra energía reduciendo el rendimiento del equipo siempre que sea posible.",
            PowerSchemeType.UltimatePerformance => "Proporciona el máximo rendimiento eliminando microrretrasos y core parking en equipos y estaciones de trabajo.",
            _ => "Plan de energía personalizado configurado por el usuario o fabricante."
        };
    }

    private static class NativeMethods
    {
        public enum PowerDataAccessor : uint
        {
            ACCESS_AC_POWER_SETTING_INDEX = 0,
            ACCESS_DC_POWER_SETTING_INDEX = 1,
            ACCESS_FRIENDLY_NAME = 2,
            ACCESS_DESCRIPTION = 3,
            ACCESS_POSSIBLE_POWER_SETTING = 4,
            ACCESS_POSSIBLE_POWER_SETTING_FRIENDLY_NAME = 5,
            ACCESS_POSSIBLE_POWER_SETTING_DESCRIPTION = 6,
            ACCESS_DEFAULT_AC_POWER_SETTING = 7,
            ACCESS_DEFAULT_DC_POWER_SETTING = 8,
            ACCESS_POSSIBLE_VALUE_MIN = 9,
            ACCESS_POSSIBLE_VALUE_MAX = 10,
            ACCESS_POSSIBLE_VALUE_INCREMENT = 11,
            ACCESS_POSSIBLE_VALUE_UNITS = 12,
            ACCESS_ICON_RESOURCE = 13,
            ACCESS_DEFAULT_SECURITY_DESCRIPTOR = 14,
            ACCESS_ATTRIBUTES = 15,
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18,
            ACCESS_ACTIVE_SCHEME = 19,
            ACCESS_CREATE_SCHEME = 20
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(IntPtr userPowerKey, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(IntPtr userPowerKey, ref Guid schemeGuid);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr rootPowerKey,
            IntPtr schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            uint accessFlags,
            uint index,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PowerReadFriendlyName(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PowerReadDescription(
            IntPtr rootPowerKey,
            ref Guid schemeGuid,
            IntPtr subGroupOfPowerSettingsGuid,
            IntPtr powerSettingGuid,
            IntPtr buffer,
            ref uint bufferSize);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerDeleteScheme(IntPtr rootPowerKey, ref Guid schemeGuid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);
    }
}
