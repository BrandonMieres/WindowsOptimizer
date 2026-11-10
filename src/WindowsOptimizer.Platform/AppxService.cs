using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

public class AppxService : IAppxService
{
    private static readonly HashSet<string> EssentialApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.WindowsStore",
        "Microsoft.WindowsTerminal",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.Windows.StartMenuExperienceHost",
        "Microsoft.Windows.Client.WebExperience",
        "Microsoft.Windows.Apprep.ChxApp",
        "Microsoft.Windows.Cortana",
        "Microsoft.UI.Xaml",
        "Microsoft.VCLibs",
        "Microsoft.NET.Native",
        "Microsoft.XboxGameOverlay",
        "Microsoft.XboxGamingOverlay",
        "Microsoft.XboxIdentityProvider",
        "Microsoft.XboxSpeechToTextOverlay"
    };

    private static readonly Dictionary<string, string> KnownCodeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1527c705-839a-4832-9118-54d4Bd6a0c89"] = "Selector de archivos de Windows (FilePicker)",
        ["c5e2524a-ea46-4f67-841f-6a9465d9d515"] = "Host del Explorador de archivos (FileExplorer)",
        ["E2A4F912-2574-4A75-9BB0-0D023378592B"] = "Selector de aplicaciones predeterminadas (AppResolverUX)",
        ["F46D4000-FD22-4DB4-AC8E-4E1DDDE828FE"] = "Cuadro de sugerencias de carpetas",
        ["Microsoft.6365217CE6EB4"] = "Microsoft Defender",
        ["Microsoft.AsyncTextService"] = "Servicio de texto asíncrono de Windows",
        ["Microsoft.AAD.BrokerPlugin"] = "Complemento de cuentas corporativas (AAD)",
        ["Microsoft.AccountsControl"] = "Control de cuentas y credenciales de Windows",
        ["Microsoft.BioEnrollment"] = "Configuración biométrica de Windows Hello",
        ["Microsoft.CredDialogHost"] = "Cuadro de diálogo de credenciales de seguridad",
        ["Microsoft.LockApp"] = "Pantalla de bloqueo de Windows",
        ["Microsoft.SecHealthUI"] = "Seguridad de Windows (Windows Defender)",
        ["Microsoft.Windows.ParentalControls"] = "Control parental de Windows",
        ["Microsoft.Windows.PeopleExperienceHost"] = "Experiencia de contactos de Windows",
        ["Microsoft.Windows.PrintQueueActionCenter"] = "Cola de impresión de Windows",
        ["Microsoft.Windows.CapturePicker"] = "Selector de captura de pantalla y ventanas",
        ["Microsoft.Windows.OOBENetworkConnectionFlow"] = "Asistente de conexión de red de Windows",
        ["Microsoft.Windows.XGpuEjectDialog"] = "Expulsión segura de GPU externa",
        ["Microsoft.XboxGameCallableUI"] = "Interfaz de llamadas de juegos Xbox",
        ["MicrosoftWindows.Client.Photon"] = "Componente Photon de Windows",
        ["MicrosoftWindows.60909653.Filons"] = "Componente de archivos de Windows (Filons)",
        ["MicrosoftWindows.61869720.Voiess"] = "Componente de voz de Windows (Voiess)",
        ["MicrosoftWindows.61869721.Livtop"] = "Componente de subtítulos en vivo (Livtop)",
        ["MicrosoftWindows.61869722.Speion"] = "Componente de reconocimiento de voz (Speion)",
        ["MicrosoftWindows.61869836.InpApp"] = "Experiencia de entrada y teclado táctil (InpApp)"
    };

    public Task<List<AppxPackage>> GetInstalledAppsAsync()
    {
        return Task.Run(() =>
        {
            var apps = new List<AppxPackage>();
            
            try
            {
                PackageManager packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser("");

                foreach (var package in packages)
                {
                    bool isFramework = package.IsFramework;
                    string rawName = package.Id.Name;
                    string name = ResolveFriendlyName(package, rawName);
                    string fullName = package.Id.FullName;
                    
                    string rawPublisher = package.Id.Publisher;
                    string publisher = rawPublisher;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(package.PublisherDisplayName))
                            publisher = package.PublisherDisplayName;
                    }
                    catch { }
                    
                    string version = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}";
                    
                    bool isSystem = DetermineIfSystem(package, rawName, isFramework);

                    apps.Add(new AppxPackage
                    {
                        Name = name,
                        PackageFullName = fullName,
                        Publisher = publisher,
                        Version = version,
                        IsFramework = isFramework,
                        IsSystem = isSystem
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting AppxPackages: {ex.Message}");
            }

            return apps;
        });
    }

    private static bool DetermineIfSystem(Package package, string rawName, bool isFramework)
    {
        if (isFramework) return true;

        try
        {
            if (package.SignatureKind == PackageSignatureKind.System)
                return true;
        }
        catch { }

        try
        {
            if (package.IsResourcePackage)
                return true;
        }
        catch { }

        try
        {
            string? installPath = package.InstalledLocation?.Path;
            if (!string.IsNullOrEmpty(installPath) && installPath.Contains("SystemApps", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }

        if (Guid.TryParse(rawName, out _))
            return true;

        foreach (var essential in EssentialApps)
        {
            if (rawName.Contains(essential, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResolveFriendlyName(Package package, string rawName)
    {
        // 1. Si está en nuestro diccionario de códigos conocidos
        if (KnownCodeNames.TryGetValue(rawName, out var friendlyName))
        {
            return friendlyName;
        }

        // 2. Comprobar si DisplayName es un texto legible válido (no recurso tipo ms-resource)
        try
        {
            if (!string.IsNullOrWhiteSpace(package.DisplayName) && 
                !package.DisplayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) &&
                !package.DisplayName.StartsWith("@{", StringComparison.OrdinalIgnoreCase))
            {
                return package.DisplayName;
            }
        }
        catch { }

        // 3. Si el nombre es un GUID o código interno, intentar deducirlo de la carpeta de instalación
        if (Guid.TryParse(rawName, out _) || rawName.StartsWith("MicrosoftWindows.", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string? folder = package.InstalledLocation?.Path;
                if (!string.IsNullOrEmpty(folder))
                {
                    string dirName = Path.GetFileName(folder);
                    int underscore = dirName.IndexOf('_');
                    if (underscore > 0)
                    {
                        string cleanDir = dirName[..underscore];
                        if (KnownCodeNames.TryGetValue(cleanDir, out var cleanFriendly))
                        {
                            return cleanFriendly;
                        }

                        // Quitar prefijos comunes como Microsoft.Windows.
                        cleanDir = cleanDir.Replace("Microsoft.Windows.", "").Replace("MicrosoftWindows.", "");
                        if (!string.IsNullOrWhiteSpace(cleanDir))
                        {
                            return cleanDir;
                        }
                    }
                }
            }
            catch { }
        }

        return rawName;
    }

    public async Task<bool> RemoveAppAsync(string packageFullName)
    {
        try
        {
            PackageManager packageManager = new PackageManager();
            var deploymentOperation = packageManager.RemovePackageAsync(packageFullName);
            
            await deploymentOperation.AsTask();
            return deploymentOperation.Status == Windows.Foundation.AsyncStatus.Completed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing AppxPackage {packageFullName}: {ex.Message}");
            return false;
        }
    }
}
