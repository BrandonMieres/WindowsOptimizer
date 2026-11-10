# Catálogo de optimizaciones: qué buscar y qué hacer

> Complemento de `PROYECTO.md`. Recoge qué servicios, tareas, claves de registro, apps y carpetas tocan los proyectos de referencia, y la lista de comprobaciones que debería tener nuestro programa.

## Cómo leer este documento

**Leyenda de origen**

| Marca | Significado |
|---|---|
| **[W]** | Visto en la documentación oficial de WinUtil (`tweaks.json` y páginas de `winutil.christitus.com`) |
| **[O]** | Visto en el README de Optimizer (hellzerg) |
| **[D]** | Visto en la documentación de Win11Debloat |
| **[G]** | Conocimiento general de Windows, **pendiente de verificar** en las fuentes o en una VM antes de convertirlo en regla |

**Importante:** los valores concretos (rutas de registro, nombres de servicios) están copiados de la documentación de WinUtil tal como aparece publicada. Pueden cambiar entre versiones, así que cada regla debe probarse en una VM y revisarse contra la versión actual del repositorio antes de usarse.

**Columnas "Nota":** salvo indicación contraria, son conocimiento general **[G]**, pendiente de verificar.

**Niveles de riesgo:** 🟢 bajo (seguro para casi todos) · 🟡 medio (depende del equipo/uso) · 🔴 alto (solo avanzado, con aviso claro).

---

## 1. Servicios

### 1.1 Los que modifica WinUtil ("Services - Set to Manual") **[W]**

| Servicio | Cambio de WinUtil | Estado original | Riesgo | Nota |
|---|---|---|---|---|
| `DiagTrack` (Connected User Experiences and Telemetry) | Disabled | Automatic | 🟢 | Telemetría |
| `MapsBroker` (mapas descargados) | Manual | Automatic | 🟢 | Solo útil si usas mapas sin conexión |
| `StorSvc` (Storage Service) | Manual | Automatic | 🟡 | **[G]** Revisar impacto en Storage Sense |
| `CscService` (archivos sin conexión) | Disabled | Manual | 🟢 | Solo útil en entornos corporativos |
| `SharedAccess` | Disabled | Automatic | 🟡 | **[G]** Parece ser Internet Connection Sharing; verificar si afecta al punto de acceso móvil |

Además ajusta el valor de registro `SvcHostSplitThresholdInKB` (en `HKLM\SYSTEM\CurrentControlSet\Control`) según la memoria RAM instalada, para reducir el número de procesos `svchost.exe`. La documentación lo calcula a partir de `Win32_PhysicalMemory` (WMI).

### 1.2 Candidatos mencionados en un issue de WinUtil (comparación con otro script) **[W: issue #5131]**

Servicios que otro script desactiva y WinUtil no: `dmwappushservice`, `SysMain` (Superfetch), `WSearch` (indexador), `HomeGroupListener` / `HomeGroupProvider`, `WMPNetworkSvc`, `diagnosticshub.standardcollector.service`, `OneSyncSvc`.

> ⚠️ Son **candidatos a evaluar**, no recomendaciones. Desactivar `SysMain` o `WSearch` suele tener efectos distintos según SSD/HDD y uso, y los mantenedores de WinUtil no los incluyen. Nuestro programa debería **detectar el tipo de disco y el uso real** antes de proponerlos.

### 1.3 Qué debe hacer nuestro analizador con los servicios

- [ ] Listar todos los servicios con: estado, tipo de inicio, cuenta, ruta del ejecutable, dependencias y si es de Microsoft
- [ ] Marcar los que coinciden con la base de reglas (con su estado original esperado)
- [ ] **Comprobar dependencias** antes de proponer un cambio (qué servicios dependen de él)
- [ ] Guardar el tipo de inicio original antes de cambiarlo (WinUtil guarda `OriginalType` y, según su código, respeta cambios previos del usuario)
- [ ] Detectar servicios de terceros con inicio automático que consumen recursos (sin proponer desactivarlos a ciegas)

---

## 2. Tareas programadas de telemetría **[W]**

Todas se deshabilitan en el tweak "Disable Telemetry" (estado original: `Enabled`):

```
\Microsoft\Windows\Autochk\Proxy
\Microsoft\Windows\Customer Experience Improvement Program\Consolidator
\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip
\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector
\Microsoft\Windows\Feedback\Siuf\DmClient
\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload
\Microsoft\Windows\Windows Error Reporting\QueueReporting
\Microsoft\Windows\Application Experience\MareBackup
\Microsoft\Windows\Application Experience\StartupAppTask
\Microsoft\Windows\Application Experience\PcaPatchDbTask
```

Versiones anteriores de la documentación también incluían `Microsoft Compatibility Appraiser` y `ProgramDataUpdater` (Application Experience). Al ser una lista que cambia, **nuestro motor debe leerla de un fichero de reglas**, no tenerla fija en el código.

Comprobaciones del analizador:
- [ ] Estado de cada tarea (habilitada/deshabilitada) con la API de Task Scheduler
- [ ] Detectar tareas de terceros con ejecución frecuente o al inicio (actualizadores, telemetría de apps)

---

## 3. Claves de registro **[W]** (tweak "Disable Telemetry")

> Formato: ruta · nombre · valor aplicado · valor original. `<RemoveEntry>` significa que el original era "no existía la clave" y el undo la borra.
>
> **Versiones de la documentación:** la lista sale de la documentación de desarrollo de WinUtil ("dev"). La documentación estable y las versiones antiguas difieren en algunas claves, por lo que hay que contrastar con el repositorio actual.
>
> **⚠️ Valor aplicado igual al original:** en algunas filas (marcadas con ⚠️) la documentación indica el mismo valor al aplicar y al restaurar, lo que no produce ningún cambio. Puede ser un error de la documentación o una regla sin efecto; verificar en el repositorio antes de convertirlas en reglas.

### 3.1 Telemetría y privacidad 🟢

| Ruta | Nombre | Aplicar | Original |
|---|---|---|---|
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection` | `AllowTelemetry` | 0 | `<RemoveEntry>` |
| `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection` | `AllowTelemetry` | 0 | `<RemoveEntry>` |
| `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection` | `DoNotShowFeedbackNotifications` | 1 | `<RemoveEntry>` |
| `HKCU\SOFTWARE\Policies\Microsoft\Windows\CloudContent` | `DisableTailoredExperiencesWithDiagnosticData` | 1 | `<RemoveEntry>` |
| `HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo` | `DisabledByGroupPolicy` | 1 | `<RemoveEntry>` |
| `HKCU\SOFTWARE\Microsoft\Siuf\Rules` | `NumberOfSIUFInPeriod` ⚠️ | 0 | 0 |
| `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting` | `Disabled` | 1 | 0 |

Otra versión de la documentación añade, en `HKCU`: `AdvertisingInfo\Enabled=0`, `Privacy\TailoredExperiencesWithDiagnosticDataEnabled=0`, `Speech_OneCore\Settings\OnlineSpeechPrivacy\HasAccepted=0`, `Input\TIPC\Enabled=0` e `InputPersonalization\RestrictImplicitInkCollection=1`.

Además ejecuta un script: `Set-MpPreference -SubmitSamplesConsent 2` (deja de enviar muestras automáticas de Defender) y desactiva la telemetría de PowerShell 7 con la variable de entorno `POWERSHELL_TELEMETRY_OPTOUT=1`. **Nota de la propia documentación:** este tweak bloquea muchos ajustes de Edge.

### 3.2 Contenido sugerido y publicidad (ContentDeliveryManager) 🟢

Todas en `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager`, valor aplicado `0` (original `1`):

`ContentDeliveryAllowed`, `OemPreInstalledAppsEnabled`, `PreInstalledAppsEnabled`, `PreInstalledAppsEverEnabled`, `SilentInstalledAppsEnabled`, `SubscribedContent-338387Enabled`, `SubscribedContent-338388Enabled`, `SubscribedContent-338389Enabled`, `SubscribedContent-353698Enabled`, `SystemPaneSuggestionsEnabled`.

También: `HKCU\...\UserProfileEngagement\ScoobeSystemSettingEnabled` = 0.

### 3.3 Interfaz y comportamiento 🟢/🟡

| Ruta | Nombre | Aplicar | Original | Riesgo |
|---|---|---|---|---|
| `HKCU\...\Explorer\Advanced` | `ShowTaskViewButton` | 0 | 1 | 🟢 |
| `HKCU\...\Explorer\Advanced\People` | `PeopleBand` | 0 | 1 | 🟢 |
| `HKCU\...\Explorer\Advanced` | `LaunchTo` ⚠️ | 1 | 1 | 🟢 |
| `HKCU\...\Policies\Explorer` | `HideSCAMeetNow` | 1 | `<RemoveEntry>` | 🟢 |
| `HKCU\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds` | `EnableFeeds` | 0 | `<RemoveEntry>` | 🟢 |
| `HKCU\...\Feeds` | `ShellFeedsTaskbarViewMode` | 2 | 1 | 🟢 |
| `HKCU\...\Explorer\OperationStatusManager` | `EnthusiastMode` | 1 | 0 | 🟢 |
| `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem` | `LongPathsEnabled` | 1 | 0 | 🟢 |
| `HKCU\Control Panel\Desktop` | `AutoEndTasks` ⚠️ | 1 | 1 | 🟡 |

### 3.4 Rendimiento y red (resultados discutibles) 🟡

| Ruta | Nombre | Aplicar | Original |
|---|---|---|---|
| `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile` | `SystemResponsiveness` | 0 | 1 |
| `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile` | `NetworkThrottlingIndex` | 4294967295 | 1 |
| `HKLM\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters` | `IRPStackSize` | 30 | 20 |
| `HKLM\SYSTEM\ControlSet001\Services\Ndu` | `Start` | 2 | 1 |
| `HKLM\...\DeliveryOptimization\Config` y `...\Policies\...\DeliveryOptimization` | `DODownloadMode` | 0 | 1 / `<RemoveEntry>` |
| `HKLM\SYSTEM\CurrentControlSet\Control\Remote Assistance` | `fAllowToGetHelp` | 0 | 1 |
| `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management` | `ClearPageFileAtShutdown` ⚠️ | 0 | 0 |

> ⚠️ **Honestidad sobre el rendimiento:** estos ajustes (`SystemResponsiveness`, `NetworkThrottlingIndex`, `IRPStackSize`) son populares, pero su mejora real es discutida y no hay consenso. Propuesta: no mostrar un porcentaje de mejora para ellos ("impacto no estimable") y dejarlos solo en el preset Advanced, salvo que las pruebas en VM demuestren un efecto medible.

### 3.5 Qué debe hacer nuestro analizador con el registro

- [ ] Leer cada clave de la regla y comparar con el valor "bueno"
- [ ] Distinguir **HKCU** (por usuario) de **HKLM** (sistema); para HKCU decidir si aplica al usuario actual, a todos o al perfil por defecto
- [ ] Detectar si la clave está **forzada por una directiva de grupo o MDM** (en ese caso no tocar y avisar)
- [ ] Guardar el valor original (o "no existía") antes de modificar
- [ ] Ofrecer exportar a `.reg` como copia de seguridad

---

## 4. Tweaks completos de WinUtil (lista de funciones) **[W]**

### 4.1 Esenciales
Crear punto de restauración · Debloat de Edge · Borrar temporales · Desactivar historial de actividad · Desactivar ConsumerFeatures · Desactivar GameDVR · Desactivar hibernación (o ponerla como predeterminada en portátiles) · Desactivar Homegroup · Desactivar localización · Desactivar telemetría de PowerShell 7 · Desactivar Storage Sense · Desactivar telemetría · Desactivar Wi-Fi Sense · Terminar tarea con clic derecho · Preferir IPv4 sobre IPv6 · Ejecutar Liberador de espacio en disco · Servicios en manual

La FAQ de WinUtil considera "seguros para todos": **desactivar telemetría, historial de actividad, localización y borrar temporales**.

### 4.2 Avanzados (con aviso) 🔴/🟡
Adobe debloat · Bloqueo de red de Adobe · Desactivar apps en segundo plano · Desactivar optimizaciones de pantalla completa · Desactivar Intel MM (vPro LMS) · Desactivar IPv6 · Desactivar Copilot · Desactivar bandeja de notificaciones/calendario · Desactivar Teredo · Cambiar DNS · **Eliminar TODAS las apps de la Store (marcado "NO RECOMENDADO")** · Quitar Inicio y Galería del Explorador · **Quitar Edge** · **Quitar OneDrive** · Ejecutar OO ShutUp10 · Menú contextual clásico · Pantalla para rendimiento · Hora en UTC (arranque dual)

### 4.3 Preferencias de personalización 🟢
Búsqueda Bing en Inicio · Centrar barra de tareas · Tema oscuro · BSoD detallado · Aceleración del ratón · NumLock al arrancar · Botón de búsqueda · Extensiones de archivo visibles · Archivos ocultos · Snap Assist (flyout / sugerencias / ajuste) · Sticky Keys · Botón Vista de tareas · Mensajes detallados al iniciar sesión · Botón de Widgets

### 4.4 Planes de energía
WinUtil incluye "Performance Plans" (por ejemplo el plan de máximo rendimiento).

---

## 5. Funciones de Optimizer (hellzerg) **[O]**

| Área | Funciones |
|---|---|
| Rendimiento | Mejoras de sistema y red · tweaks avanzados (por ejemplo HPET) |
| Servicios | Desactivar servicios innecesarios |
| Privacidad | Telemetría de Windows y de Office 2016+ · Cortana · Copilot (Windows 11 y Edge) |
| Actualizaciones | Detener actualizaciones automáticas (no recomendado por seguridad) |
| Apps | Desinstalar apps UWP · descargar apps útiles · OneDrive |
| Limpieza | Unidad del sistema y **perfiles de navegador** · corrección de problemas de registro |
| Red | Cambiar DNS desde lista · vaciar caché DNS · ping y latencia · búsqueda de IP |
| Sistema | Programas de inicio · editar `hosts` · **detectar y cerrar manejadores de archivos bloqueados** · inspección de hardware · menú contextual · hora UTC |

---

## 6. Apps preinstaladas y bloatware **[D]**

Win11Debloat permite elegir qué apps eliminar. Modos descritos en su documentación: **Default** (selección predefinida), **Lite** (no elimina apps) y **Custom** (selección manual).

- **Verificado:** la selección por defecto incluye, entre otras, Microsoft Teams, Spotify y Sticky Notes. Windows Terminal **no** se elimina por defecto. Las apps eliminadas se pueden reinstalar desde Microsoft Store, hay guía para revertir cambios y la copia de seguridad del menú Inicio lleva marca de tiempo.
- **Pendiente de verificar:** la lista completa. La versión de junio de 2026 indica que se eliminó el soporte del generador antiguo de listas y del fichero `CustomAppsList`, así que el antiguo `AppsList.txt` podría haber cambiado de formato o desaparecido. Consultar la lista actual en el repositorio y su wiki (sección App Removal).

Qué debe hacer nuestro analizador:
- [ ] Listar apps UWP/MSIX instaladas (por usuario y para todos) con tamaño y editor
- [ ] Clasificar: "sistema, no tocar" / "preinstalada opcional" / "de terceros"
- [ ] **Nunca** eliminar por defecto: Store, Terminal, componentes de sistema (WinUtil marca "eliminar todas" como no recomendado)
- [ ] Permitir selección individual y mostrar cómo reinstalar

---

## 7. Limpieza de archivos (referencia BleachBit/winapp2 **[G]** + WinUtil **[W]**)

WinUtil ofrece "Borrar temporales" y "Ejecutar Liberador de espacio en disco". BleachBit/winapp2 definen miles de reglas de limpieza por aplicación con detección ("solo si existe la ruta"). Ubicaciones habituales a evaluar **[G, verificar]**:

| Elemento | Ubicación típica | Riesgo |
|---|---|---|
| Temporales de usuario | `%TEMP%` | 🟢 |
| Temporales del sistema | `C:\Windows\Temp` | 🟢 |
| Caché de actualizaciones descargadas | `C:\Windows\SoftwareDistribution\Download` | 🟡 |
| Miniaturas | `%LOCALAPPDATA%\Microsoft\Windows\Explorer` (thumbcache) | 🟢 |
| Volcados y informes de errores | `%LOCALAPPDATA%\CrashDumps`, `C:\ProgramData\Microsoft\Windows\WER` | 🟢 |
| Caché de navegadores | Perfiles de Chrome, Edge, Firefox | 🟢 (no tocar contraseñas ni sesiones sin avisar) |
| Papelera de reciclaje | `$Recycle.Bin` | 🟢 |
| Registros de Windows | `C:\Windows\Logs`, CBS | 🟡 |
| Prefetch | `C:\Windows\Prefetch` | 🟡 (borrarlo no suele acelerar nada) |

Reglas de diseño tomadas de BleachBit:
- [ ] Cada regla de limpieza con **detección** previa y **vista previa** del espacio recuperable
- [ ] Mostrar siempre lo que se va a borrar antes de borrarlo
- [ ] Agrupar escaneos del mismo directorio para recorrerlo una sola vez
- [ ] Excluir archivos en uso y manejar los bloqueados sin forzar

---

## 8. Red y DNS **[W][O][G]**

- [ ] DNS actual de cada adaptador y comparación con proveedores de la lista (WinUtil tiene `dns.json`; Optimizer tiene lista de DNS)
- [ ] Cambio de DNS (Google, Cloudflare, etc.) con **restauración al DNS original**
- [ ] Vaciar caché DNS (**[O]**)
- [ ] Medir latencia/ping a varios servidores antes y después (**[O]**)
- [ ] Preferencia IPv4/IPv6, Teredo (**[W]**, avanzado)
- [ ] Conexiones activas por proceso y puertos a la escucha (**[G]**) para detectar procesos sospechosos o ruidosos
- [ ] Revisar archivo `hosts` (**[O]**) y avisar de entradas inesperadas

---

## 9. Arranque, drivers y hardware

| Comprobación | Fuente | Detalle |
|---|---|---|
| Programas de inicio | **[O]** | Carpeta Inicio, claves `Run`/`RunOnce`, tareas al iniciar sesión **[G]** |
| Inspección de hardware | **[O]** | CPU, RAM, discos, GPU vía WMI/CIM |
| Tipo de disco (SSD/HDD) | **[G]** | Condiciona recomendaciones (SysMain, indexado, desfragmentación) |
| Drivers | **[G]** | Fecha y versión, proveedor, dispositivos con error (código de problema), drivers sin firmar |
| Plan de energía | **[W]** | Plan activo y alternativa de alto rendimiento |
| Hibernación | **[W]** | `hiberfil.sys` ocupa espacio; en portátiles puede convenir mantenerla |
| Archivo de paginación y memoria | **[G]** | Uso de RAM, procesos pesados |
| Salud del disco (SMART) | **[G]** | Alertas de fallo inminente |
| Actualizaciones pendientes | **[G]** | Estado de Windows Update y reinicios pendientes |
| Protección del sistema | **[W]** | Existencia de puntos de restauración. **[G]** Por defecto Windows limita la frecuencia con la que se crean (en torno a uno cada 24 horas; verificar), así que la app debe detectar si no se pudo crear y avisar antes de aplicar cambios |

---

## 10. Qué NO debe hacer nuestro programa (o solo con aviso fuerte)

Aprendido de las advertencias de los propios proyectos:

| Acción | Motivo |
|---|---|
| Eliminar **todas** las apps de la Store | WinUtil lo marca "NO RECOMENDADO" |
| Desactivar Windows Update de forma permanente | Riesgo de seguridad; WinUtil recomienda desactivarlo solo en periodos críticos |
| Quitar Edge o Defender | Puede romper componentes del sistema |
| "Limpiadores de registro" masivos | Poco beneficio y riesgo real |
| Aplicar tweaks sin punto de restauración | Sin vuelta atrás |
| Tocar claves forzadas por directiva (empresa/MDM) | Pueden revertirse o generar conflictos |
| Prometer mejoras de rendimiento sin medirlas | Muchos ajustes populares no tienen efecto probado |
| Cambios sin explicar qué hacen | La transparencia es principio de privacy.sexy y WinUtil |

---

## 11. Resumen: qué hará el programa (flujo)

1. **Detectar entorno**: versión/compilación de Windows, edición, tipo de equipo (portátil/sobremesa), SSD/HDD, si está gestionado por empresa.
2. **Analizar** (solo lectura): servicios, tareas, registro, apps, archivos temporales, red/DNS, arranque, drivers, energía, disco.
3. **Evaluar con reglas**: cada hallazgo con riesgo, explicación, fuente y **estimación orientativa de mejora** (rango aproximado y confianza, o "no estimable").
4. **Mostrar el informe**: puntuación de salud y lista filtrable por categoría y riesgo.
5. **Aplicar**: punto de restauración (avisando si no se pudo crear) → snapshot de valores originales → cambio → registro.
6. **Verificar**: comprobar que el cambio se aplicó, que no hay efectos secundarios (red local, impresoras, actualizaciones) y, opcionalmente, volver a medir para calibrar las estimaciones.
7. **Deshacer**: desde el historial, cambio a cambio o completo.
8. **Vigilar la deriva**: detectar ajustes que Windows Update haya revertido y ofrecer reaplicarlos.

---

## 12. Pendiente de revisar en las fuentes

- [ ] Leer `config/tweaks.json` **completo** en el repositorio actual de WinUtil (aquí solo hemos visto los tweaks de servicios y telemetría)
- [ ] Leer `config/preset.json` para ver qué incluye cada preset
- [ ] Ver las secciones `appx` y `ScriptType` / `UndoScript` de WinUtil (cómo deshace los cambios que no son de registro)
- [ ] Revisar la lista actual de apps de Win11Debloat (el antiguo `AppsList` pudo cambiar en la versión de junio de 2026) y su guía de reversión
- [ ] Estudiar los módulos de limpieza de Optimizer (código C#)
- [ ] Revisar las reglas de `winapp2.ini` para rutas de limpieza reales
- [ ] Verificar en VM cada valor de registro de las secciones 3.1 a 3.4, empezando por las filas marcadas con ⚠️
- [ ] Comprobar en el repositorio actual qué claves del tweak de telemetría siguen vigentes (la documentación estable y la de desarrollo difieren)
- [ ] Confirmar licencias antes de reutilizar nada de forma literal

---

## 13. Relación con las reglas y la estimación de mejora

Cada elemento de este catálogo se convertirá en una regla de `rules/*.json` (esquema en `PROYECTO.md`, sección 6.4). Correspondencia propuesta:

| Sección del catálogo | Archivo de reglas | Tipo de impacto (`impact.type`) | Estimación |
|---|---|---|---|
| 1. Servicios | `services.json` | `ram` (memoria y CPU del servicio medidos en el equipo) | Media |
| 2. Tareas programadas | `scheduled-tasks.json` | `privacy` | Sin porcentaje |
| 3.1 / 3.2 Registro de telemetría y contenido sugerido | `privacy.json` | `privacy` | Sin porcentaje |
| 3.3 Registro de interfaz | `performance.json` | `none` | No estimable |
| 3.4 Registro de rendimiento y red | `performance.json` / `network.json` | `none` (efecto no demostrado) | No estimable, solo preset Advanced |
| 4.3 Preferencias de personalización | `performance.json` | `none` | No estimable |
| 5 y 8. DNS y red | `network.json` | `latency` (medida frente a DNS alternativos) | Media |
| 6. Apps preinstaladas | `apps.json` | `disk_space` | Alta (el tamaño es real) |
| 7. Limpieza de archivos | `cleaners.json` | `disk_space` | Alta (el tamaño es real) |
| 9. Programas de arranque | `startup.json` | `boot_time` (contribución medida al arranque) | Media o baja |
| 9. Plan de energía y modo juego | `gaming.json` | `none` | No estimable |

Regla general: **solo se muestra un porcentaje cuando hay datos medidos en el equipo que lo respalden**. En el resto se muestra la etiqueta "impacto no estimable" o el motivo del cambio (por ejemplo, "reduce el envío de datos").

---

## Fuentes consultadas

- WinUtil: https://github.com/ChrisTitusTech/winutil · documentación https://winutil.christitus.com/ (páginas de tweaks de telemetría, servicios, arquitectura y FAQ) · issue #5131
- Optimizer: https://github.com/hellzerg/optimizer
- Win11Debloat: https://github.com/Raphire/Win11Debloat
- BleachBit / winapp2.ini: https://docs.bleachbit.org/ · https://github.com/bleachbit/winapp2.ini
