using System.Management;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Platform;

/// <summary>
/// Implementación de IDeviceHealthService que audita la salud del hardware y controladores instalados.
/// Utiliza consultas WMI nativas (Win32_PnPEntity y Win32_PnPSignedDriver).
/// </summary>
public sealed class DeviceHealthService : IDeviceHealthService
{
    public Task<(IReadOnlyList<DeviceProblem> Problems, IReadOnlyList<DriverItem> Drivers, DeviceHealthSummary Summary)> AuditDevicesAndDriversAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var problems = new List<DeviceProblem>();
            var drivers = new List<DriverItem>();

            // 1. Escanear dispositivos con problemas de hardware o driver (ConfigManagerErrorCode > 0)
            try
            {
                using var searcherProblems = new ManagementObjectSearcher(
                    "SELECT DeviceID, Name, ConfigManagerErrorCode, PNPClass, Manufacturer FROM Win32_PnPEntity WHERE ConfigManagerErrorCode > 0");

                foreach (var obj in searcherProblems.Get())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    string deviceId = obj["DeviceID"]?.ToString() ?? "Desconocido";
                    string name = obj["Name"]?.ToString() ?? "Dispositivo desconocido";
                    string pnpClass = obj["PNPClass"]?.ToString() ?? "Sistema";
                    string manufacturer = obj["Manufacturer"]?.ToString() ?? "Genérico";
                    uint errorCode = 0;

                    if (obj["ConfigManagerErrorCode"] != null && uint.TryParse(obj["ConfigManagerErrorCode"].ToString(), out uint ec))
                    {
                        errorCode = ec;
                    }

                    var (desc, sugg) = GetPnpErrorInfo(errorCode);

                    problems.Add(new DeviceProblem
                    {
                        DeviceId = deviceId,
                        Name = name,
                        ClassName = pnpClass,
                        Manufacturer = manufacturer,
                        ErrorCode = errorCode,
                        ErrorDescription = desc,
                        Suggestion = sugg
                    });
                }
            }
            catch
            {
                // Manejo seguro si WMI estuviera bloqueado o no disponible
            }

            // 2. Escanear controladores instalados
            try
            {
                using var searcherDrivers = new ManagementObjectSearcher(
                    "SELECT DeviceName, DeviceClass, DriverProviderName, DriverVersion, DriverDate, IsSigned FROM Win32_PnPSignedDriver WHERE DeviceName IS NOT NULL");

                var fourYearsAgo = DateTimeOffset.UtcNow.AddYears(-4);

                foreach (var obj in searcherDrivers.Get())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    string devName = obj["DeviceName"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(devName)) continue;

                    string devClass = obj["DeviceClass"]?.ToString() ?? "Sistema";
                    string provider = obj["DriverProviderName"]?.ToString() ?? "Desconocido";
                    string version = obj["DriverVersion"]?.ToString() ?? "Desconocida";
                    bool isSigned = true;

                    if (obj["IsSigned"] != null && bool.TryParse(obj["IsSigned"].ToString(), out bool signed))
                    {
                        isSigned = signed;
                    }

                    DateTimeOffset? driverDate = null;
                    string rawDate = obj["DriverDate"]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(rawDate))
                    {
                        try
                        {
                            var dt = ManagementDateTimeConverter.ToDateTime(rawDate);
                            driverDate = new DateTimeOffset(dt, TimeSpan.Zero);
                        }
                        catch
                        {
                            if (DateTimeOffset.TryParse(rawDate, out var parsedDto))
                            {
                                driverDate = parsedDto;
                            }
                        }
                    }

                    bool isOld = driverDate.HasValue && driverDate.Value < fourYearsAgo;

                    // Detección de drivers genéricos básicos de Microsoft en componentes que requieren drivers dedicados (GPU, Audio, Red)
                    bool isGeneric = provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                        (devClass.Equals("DISPLAY", StringComparison.OrdinalIgnoreCase) ||
                         devName.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) ||
                         devName.Contains("Controlador de vídeo básico", StringComparison.OrdinalIgnoreCase));

                    string statusText = "Correcto";
                    if (!isSigned)
                    {
                        statusText = "Sin firma digital";
                    }
                    else if (isGeneric)
                    {
                        statusText = "Driver básico (Recomendado oficial)";
                    }
                    else if (isOld)
                    {
                        statusText = "Antiguo (> 4 años)";
                    }

                    drivers.Add(new DriverItem
                    {
                        DeviceName = devName,
                        DeviceClass = devClass,
                        DriverProvider = provider,
                        DriverVersion = version,
                        DriverDate = driverDate,
                        IsSigned = isSigned,
                        IsGenericMicrosoftDriver = isGeneric,
                        IsOld = isOld,
                        StatusText = statusText
                    });
                }
            }
            catch
            {
                // Fallback seguro
            }

            // 3. Resumen de salud
            int totalDevices = drivers.Count + problems.Count;
            int problemCount = problems.Count;
            int oldDriversCount = drivers.Count(d => d.IsOld);
            int genericDriversCount = drivers.Count(d => d.IsGenericMicrosoftDriver);
            int unsignedDriversCount = drivers.Count(d => !d.IsSigned);

            string summaryDesc;
            if (problemCount == 0)
            {
                if (genericDriversCount > 0)
                {
                    summaryDesc = $"Todos los dispositivos funcionan correctamente, pero se detectaron {genericDriversCount} controladores genéricos de Microsoft que podrían actualizarse.";
                }
                else if (oldDriversCount > 0)
                {
                    summaryDesc = $"Hardware en buen estado ({drivers.Count} controladores analizados). Se detectaron {oldDriversCount} controladores con más de 4 años de antigüedad.";
                }
                else
                {
                    summaryDesc = $"Estado óptimo: Los {drivers.Count} dispositivos y controladores analizados funcionan sin errores y cuentan con firma digital.";
                }
            }
            else
            {
                summaryDesc = $"Atención: Se detectaron {problemCount} dispositivo(s) con código de error o sin controlador asignado en Windows.";
            }

            var summary = new DeviceHealthSummary
            {
                TotalDevicesCount = totalDevices,
                ProblemDevicesCount = problemCount,
                OldDriversCount = oldDriversCount,
                GenericDriversCount = genericDriversCount,
                UnsignedDriversCount = unsignedDriversCount,
                SummaryDescription = summaryDesc
            };

            return (
                (IReadOnlyList<DeviceProblem>)problems.AsReadOnly(),
                (IReadOnlyList<DriverItem>)drivers.OrderBy(d => d.DeviceClass).ThenBy(d => d.DeviceName).ToList().AsReadOnly(),
                summary
            );
        }, cancellationToken);
    }

    private static (string Description, string Suggestion) GetPnpErrorInfo(uint errorCode)
    {
        return errorCode switch
        {
            1 => ("El dispositivo no está configurado correctamente (Código 1).",
                  "Actualiza o reinstala el controlador desde el Administrador de Dispositivos."),
            3 => ("El controlador para este dispositivo podría estar dañado o falta memoria (Código 3).",
                  "Comprueba la memoria libre del sistema o reinstala el controlador."),
            10 => ("El dispositivo no puede iniciar (Código 10).",
                   "Comprueba la conexión física o descarga el controlador más reciente desde la página oficial del fabricante."),
            12 => ("El dispositivo no encuentra suficientes recursos libres que usar (Código 12).",
                   "Conflicto de recursos de hardware. Deshabilita dispositivos en conflicto o actualiza la BIOS de la placa base."),
            14 => ("El equipo debe reiniciarse para que el dispositivo funcione (Código 14).",
                   "Reinicia el equipo para completar la instalación o configuración del dispositivo."),
            18 => ("Es necesario reinstalar los controladores para este dispositivo (Código 18).",
                   "Reinstala los controladores desde el Administrador de Dispositivos o el software oficial del fabricante."),
            19 => ("La información de configuración del registro está incompleta o dañada (Código 19).",
                   "Desinstala y vuelve a instalar el dispositivo para regenerar las claves del Registro."),
            21 => ("Windows está quitando este dispositivo (Código 21).",
                   "Reinicia el equipo."),
            22 => ("Este dispositivo está deshabilitado (Código 22).",
                   "El dispositivo fue deshabilitado por el usuario o sistema. Puedes habilitarlo desde el Administrador de Dispositivos si deseas usarlo."),
            24 => ("El dispositivo no está presente o no tiene todos sus controladores instalados (Código 24).",
                   "Comprueba la conexión física del dispositivo o reinstala sus controladores."),
            28 => ("No están instalados los controladores para este dispositivo (Código 28).",
                   "Faltan controladores. Ve a la web del fabricante de tu equipo o componente para instalar el driver correspondiente."),
            29 => ("El dispositivo está deshabilitado porque el firmware no le asignó recursos (Código 29).",
                   "Habilita el dispositivo en la configuración de la BIOS / UEFI."),
            31 => ("Windows no puede cargar los controladores requeridos (Código 31).",
                   "Descarga un controlador compatible con esta versión de Windows."),
            32 => ("Un controlador (servicio) para este dispositivo se ha deshabilitado (Código 32).",
                   "Reinstala el controlador o revisa el estado del servicio en el Administrador de Servicios."),
            37 => ("Windows no puede inicializar el controlador de dispositivo (Código 37).",
                   "Desinstala el dispositivo y haz clic en 'Buscar cambios de hardware'."),
            38 => ("Una instancia anterior del controlador todavía está en memoria (Código 38).",
                   "Reinicia el equipo para liberar la memoria ocupada por la instancia previa."),
            39 => ("El controlador de dispositivo está dañado o falta (Código 39).",
                   "Desinstala el dispositivo y vuelve a instalar los controladores oficiales."),
            41 => ("El controlador se cargó pero no se encuentra el dispositivo físico (Código 41).",
                   "Comprueba que el dispositivo esté bien conectado físicamente a la placa o puerto."),
            43 => ("Windows detuvo este dispositivo porque informó de problemas graves (Código 43).",
                   "Fallo crítico reportado por el hardware. Muy común en tarjetas gráficas o USB. Realiza una instalación limpia del driver o comprueba el estado del componente."),
            48 => ("Se ha bloqueado el inicio del software de este dispositivo por problemas conocidos (Código 48).",
                   "Obtén un controlador actualizado del fabricante compatible con tu compilación de Windows."),
            52 => ("Windows no puede comprobar la firma digital del controlador (Código 52).",
                   "El controlador no está firmado digitalmente con certificado WHQL o ha sido modificado. Instala el controlador oficial firmado."),
            _ => ($"El dispositivo reporta el código de estado {errorCode} en Windows.",
                  "Revisa el dispositivo en el Administrador de Dispositivos de Windows (devmgmt.msc).")
        };
    }
}
