using System.Runtime.Versioning;
using System.ServiceProcess;
using WindowsOptimizer.Core.Abstractions;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación real del gestor de servicios usando System.ServiceProcess.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceManager : IServiceManager
{
    public ServiceInfo? GetServiceInfo(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return new ServiceInfo
            {
                Name = sc.ServiceName,
                DisplayName = sc.DisplayName,
                StartType = MapStartTypeNative(sc.StartType),
                Status = MapStatus(sc.Status),
                Description = null // ServiceController no expone Description directamente
            };
        }
        catch (InvalidOperationException)
        {
            return null; // servicio no existe
        }
    }

    public void SetStartupType(string serviceName, Core.Abstractions.ServiceStartMode mode)
    {
        // Usamos sc.exe directamente ya que ServiceController no permite cambiar StartType
        var scMode = mode switch
        {
            Core.Abstractions.ServiceStartMode.Automatic => "auto",
            Core.Abstractions.ServiceStartMode.Manual => "demand",
            Core.Abstractions.ServiceStartMode.Disabled => "disabled",
            _ => "demand"
        };

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"config \"{serviceName}\" start= {scMode}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        process?.WaitForExit(5000);
    }

    public void StopService(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception)
        {
            // TODO: Log
        }
    }

    public void StartService(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception)
        {
            // TODO: Log
        }
    }

    public bool ServiceExists(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status; // force access to validate
            return true;
        }
        catch
        {
            return false;
        }
    }



    // Remap from System.ServiceProcess to our abstraction
    private static Core.Abstractions.ServiceStartMode MapStartTypeNative(System.ServiceProcess.ServiceStartMode native)
    {
        return native switch
        {
            System.ServiceProcess.ServiceStartMode.Automatic => Core.Abstractions.ServiceStartMode.Automatic,
            System.ServiceProcess.ServiceStartMode.Manual => Core.Abstractions.ServiceStartMode.Manual,
            System.ServiceProcess.ServiceStartMode.Disabled => Core.Abstractions.ServiceStartMode.Disabled,
            System.ServiceProcess.ServiceStartMode.Boot => Core.Abstractions.ServiceStartMode.Boot,
            System.ServiceProcess.ServiceStartMode.System => Core.Abstractions.ServiceStartMode.System,
            _ => Core.Abstractions.ServiceStartMode.Manual
        };
    }

    private static ServiceCurrentStatus MapStatus(ServiceControllerStatus native)
    {
        return native switch
        {
            ServiceControllerStatus.Running => ServiceCurrentStatus.Running,
            ServiceControllerStatus.Stopped => ServiceCurrentStatus.Stopped,
            ServiceControllerStatus.StartPending => ServiceCurrentStatus.StartPending,
            ServiceControllerStatus.StopPending => ServiceCurrentStatus.StopPending,
            _ => ServiceCurrentStatus.Unknown
        };
    }
}
