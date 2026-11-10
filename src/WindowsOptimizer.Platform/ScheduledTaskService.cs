using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Enums;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación de IScheduledTaskService mediante COM (Schedule.Service)
/// con fallback a schtasks.exe para máxima compatibilidad en Windows.
/// </summary>
public sealed class ScheduledTaskService : IScheduledTaskService
{
    public Task<ScheduledTaskState> GetTaskStateAsync(string taskPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetTaskStateInternal(taskPath), cancellationToken);
    }

    public Task<bool> SetTaskStateAsync(string taskPath, ScheduledTaskState state, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SetTaskStateInternal(taskPath, state), cancellationToken);
    }

    public Task<bool> TaskExistsAsync(string taskPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => TaskExistsInternal(taskPath), cancellationToken);
    }

    private static ScheduledTaskState GetTaskStateInternal(string taskPath)
    {
        if (string.IsNullOrWhiteSpace(taskPath))
            return ScheduledTaskState.Unknown;

        // Intentar primero mediante COM (Schedule.Service)
        try
        {
            var comResult = GetTaskStateViaCom(taskPath);
            if (comResult != ScheduledTaskState.Unknown)
            {
                return comResult;
            }
        }
        catch
        {
            // Fallback a schtasks.exe
        }

        return GetTaskStateViaCli(taskPath);
    }

    private static bool SetTaskStateInternal(string taskPath, ScheduledTaskState state)
    {
        if (string.IsNullOrWhiteSpace(taskPath))
            return false;

        if (state != ScheduledTaskState.Enabled && state != ScheduledTaskState.Disabled)
            return false;

        // Intentar primero mediante COM
        try
        {
            if (SetTaskStateViaCom(taskPath, state))
            {
                return true;
            }
        }
        catch
        {
            // Fallback a schtasks.exe
        }

        return SetTaskStateViaCli(taskPath, state);
    }

    private static bool TaskExistsInternal(string taskPath)
    {
        var state = GetTaskStateInternal(taskPath);
        return state is ScheduledTaskState.Enabled or ScheduledTaskState.Disabled;
    }

    #region COM Implementation (Schedule.Service)

    private static ScheduledTaskState GetTaskStateViaCom(string taskPath)
    {
        Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
        if (serviceType is null)
            return ScheduledTaskState.Unknown;

        object? service = null;
        object? folder = null;
        object? task = null;

        try
        {
            service = Activator.CreateInstance(serviceType);
            if (service is null) return ScheduledTaskState.Unknown;

            // service.Connect()
            serviceType.InvokeMember("Connect", System.Reflection.BindingFlags.InvokeMethod, null, service, null, System.Globalization.CultureInfo.InvariantCulture);

            var (folderPath, taskName) = SplitTaskPath(taskPath);

            // folder = service.GetFolder(folderPath)
            folder = serviceType.InvokeMember("GetFolder", System.Reflection.BindingFlags.InvokeMethod, null, service, [folderPath], System.Globalization.CultureInfo.InvariantCulture);
            if (folder is null) return ScheduledTaskState.NotFound;

            var folderType = folder.GetType();
            // task = folder.GetTask(taskName)
            task = folderType.InvokeMember("GetTask", System.Reflection.BindingFlags.InvokeMethod, null, folder, [taskName], System.Globalization.CultureInfo.InvariantCulture);
            if (task is null) return ScheduledTaskState.NotFound;

            var taskType = task.GetType();
            var enabledObj = taskType.InvokeMember("Enabled", System.Reflection.BindingFlags.GetProperty, null, task, null, System.Globalization.CultureInfo.InvariantCulture);

            if (enabledObj is bool isEnabled)
            {
                return isEnabled ? ScheduledTaskState.Enabled : ScheduledTaskState.Disabled;
            }

            return ScheduledTaskState.Unknown;
        }
        catch (COMException comEx)
        {
            // HRESULT 0x80070002 = ERROR_FILE_NOT_FOUND (la tarea no existe)
            if (comEx.HResult == unchecked((int)0x80070002))
            {
                return ScheduledTaskState.NotFound;
            }
            return ScheduledTaskState.Unknown;
        }
        catch
        {
            return ScheduledTaskState.Unknown;
        }
        finally
        {
            if (task is not null) Marshal.ReleaseComObject(task);
            if (folder is not null) Marshal.ReleaseComObject(folder);
            if (service is not null) Marshal.ReleaseComObject(service);
        }
    }

    private static bool SetTaskStateViaCom(string taskPath, ScheduledTaskState state)
    {
        Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
        if (serviceType is null)
            return false;

        object? service = null;
        object? folder = null;
        object? task = null;

        try
        {
            service = Activator.CreateInstance(serviceType);
            if (service is null) return false;

            serviceType.InvokeMember("Connect", System.Reflection.BindingFlags.InvokeMethod, null, service, null, System.Globalization.CultureInfo.InvariantCulture);

            var (folderPath, taskName) = SplitTaskPath(taskPath);

            folder = serviceType.InvokeMember("GetFolder", System.Reflection.BindingFlags.InvokeMethod, null, service, [folderPath], System.Globalization.CultureInfo.InvariantCulture);
            if (folder is null) return false;

            var folderType = folder.GetType();
            task = folderType.InvokeMember("GetTask", System.Reflection.BindingFlags.InvokeMethod, null, folder, [taskName], System.Globalization.CultureInfo.InvariantCulture);
            if (task is null) return false;

            var taskType = task.GetType();
            bool targetEnabled = (state == ScheduledTaskState.Enabled);

            taskType.InvokeMember("Enabled", System.Reflection.BindingFlags.SetProperty, null, task, [targetEnabled], System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (task is not null) Marshal.ReleaseComObject(task);
            if (folder is not null) Marshal.ReleaseComObject(folder);
            if (service is not null) Marshal.ReleaseComObject(service);
        }
    }

    #endregion

    #region CLI Implementation (schtasks.exe)

    private static ScheduledTaskState GetTaskStateViaCli(string taskPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{taskPath}\" /FO CSV /NH",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return ScheduledTaskState.Unknown;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return ScheduledTaskState.NotFound;
            }

            // El CSV tiene formato: "TaskName","Next Run Time","Status"
            // Ej: "\Microsoft\Windows\Autochk\Proxy","N/A","Listo" o "Deshabilitado" o "Disabled"
            var parts = output.Split(',');
            if (parts.Length >= 3)
            {
                string status = parts[2].Trim('"', '\r', '\n', ' ');
                if (status.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Deshabilitado", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("Desactivado", StringComparison.OrdinalIgnoreCase))
                {
                    return ScheduledTaskState.Disabled;
                }

                return ScheduledTaskState.Enabled;
            }

            return ScheduledTaskState.Unknown;
        }
        catch
        {
            return ScheduledTaskState.Unknown;
        }
    }

    private static bool SetTaskStateViaCli(string taskPath, ScheduledTaskState state)
    {
        try
        {
            string flag = state == ScheduledTaskState.Enabled ? "/ENABLE" : "/DISABLE";
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Change /TN \"{taskPath}\" {flag}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private static (string Folder, string TaskName) SplitTaskPath(string fullPath)
    {
        string normalized = fullPath.Replace('/', '\\').Trim();
        if (!normalized.StartsWith('\\'))
        {
            normalized = "\\" + normalized;
        }

        int lastSlash = normalized.LastIndexOf('\\');
        if (lastSlash <= 0)
        {
            return ("\\", normalized.TrimStart('\\'));
        }

        string folder = normalized[..lastSlash];
        string taskName = normalized[(lastSlash + 1)..];
        return (folder, taskName);
    }
}
