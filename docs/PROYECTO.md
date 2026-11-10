# Windows Optimizer (nombre provisional)

> Aplicación de escritorio para Windows que **analiza el equipo completo**, **detecta puntos de mejora** y permite **aplicarlos de forma segura y reversible**.
>
> Documentos del proyecto: `PROYECTO.md` (este) y `CATALOGO_OPTIMIZACIONES.md` (qué servicios, tareas, claves de registro y apps revisa y modifica el programa).

---

## 1. Descripción del proyecto

### Objetivo
Crear un optimizador de Windows moderno, transparente y fiable, inspirado en las mejores herramientas del mercado (CCleaner, Wise Care, Glary Utilities, O&O ShutUp10, WinUtil, Sophia Script, TreeSize/WizTree).

### Flujo principal
1. **Analizar**: el motor examina drivers, conexiones de red, archivos temporales, servicios activos, programas de arranque, telemetría, plan de energía, etc.
2. **Mostrar**: se presenta un panel con una puntuación de salud y una lista de hallazgos (gravedad, explicación y **estimación orientativa de mejora**, ver 8.2).
3. **Aplicar**: el usuario elige qué corregir; la app crea un punto de restauración y guarda los valores previos.
4. **Revertir**: cualquier cambio puede deshacerse desde el historial, uno a uno o todos a la vez.

### Módulos funcionales
| Módulo | Qué hace |
|---|---|
| Limpieza | Temporales, caché, papelera, restos de actualizaciones |
| Analizador de disco | Peso de carpetas y archivos (tipo TreeSize), duplicados y archivos grandes antiguos |
| Servicios y tareas programadas | Detecta servicios y tareas innecesarios y propone cambios |
| Arranque | Programas y tareas que ralentizan el inicio, con atribución de tiempo |
| Red | Diagnóstico de conexiones, cambio de DNS (Google / Cloudflare), ajustes TCP |
| Drivers | Estado, antigüedad y errores de dispositivos |
| Privacidad | Desactivar telemetría y funciones de seguimiento |
| Apps preinstaladas | Lista, clasifica y permite eliminar apps opcionales |
| Rendimiento y energía | Plan de energía, modo juego básico y ajustes de rendimiento |
| Memoria y procesos | Uso de RAM y procesos pesados; diagnóstico "¿por qué va lento ahora?" |
| Mediciones y estimación | Línea base del equipo y estimación orientativa de mejora |
| Historial y deriva | Cambios aplicados, restauración individual y detección de ajustes revertidos |

### Principios de diseño
- **Modo "solo analizar"** por defecto: nada se modifica sin confirmación.
- **Todo reversible**: punto de restauración + registro de valores anteriores.
- **Transparencia**: cada hallazgo explica qué es, por qué importa y qué cambia.
- **Sin promesas exageradas**: nada de "limpiadores de registro" agresivos ni "liberadores de RAM" sin efecto real.
- **Ligera**: sin Chromium embebido; rendimiento nativo.
- **Estimaciones honestas**: la mejora esperada se muestra como rango aproximado y con su nivel de confianza; si no se puede estimar, se dice.

---

## 2. Stack tecnológico

| Capa | Tecnología |
|---|---|
| Lenguaje | C# sobre la versión LTS vigente de .NET (verificar: .NET 8 deja de recibir soporte a finales de 2026) |
| UI | WinUI 3 (Windows App SDK) |
| Patrón de UI | MVVM con CommunityToolkit.Mvvm |
| Inyección de dependencias | Microsoft.Extensions.DependencyInjection / Hosting |
| Análisis del sistema | WMI/CIM (`System.Management`), Registro (`Microsoft.Win32`), `System.ServiceProcess`, `System.Diagnostics`, `IPGlobalProperties` |
| Operaciones con privilegios | Servicio de Windows (o proceso elevado) comunicado por Named Pipes |
| Persistencia local | SQLite (`Microsoft.Data.Sqlite`) |
| Reglas de análisis | Archivos JSON versionados, validados con JSON Schema |
| Mediciones | Contadores de rendimiento, `System.Diagnostics` y registro de eventos de rendimiento de arranque de Windows (verificar origen exacto de los datos) |
| Logs | Serilog |
| Pruebas | xUnit + Moq; VM de Windows con instantáneas para pruebas de sistema |
| Empaquetado | MSIX (o instalador con WiX) |
| Firma de código | Certificado de firma (evita avisos de SmartScreen) |
| CI/CD | GitHub Actions |
| Opcional | Módulo en Rust (DLL) para escaneo rápido de la MFT de NTFS |

### Backend (fase posterior: cuentas y licencias)
| Elemento | Tecnología |
|---|---|
| Base de datos | PostgreSQL en Neon |
| API | Node.js en Vercel |
| Autenticación | JWT (secret en variable de entorno `JWT_SECRET`) |
| Fechas | Siempre server-side con `now()`; columnas `TIMESTAMPTZ` |
| Dispositivos | Tabla relacional de dispositivos con MAC hasheada (máx. 3 por cuenta) |
| Tipos de cuenta | vip / deluxe / admin, con fecha de expiración |

---

## 3. Estructura de carpetas

```
WindowsOptimizer/
├── README.md
├── PROYECTO.md
├── CATALOGO_OPTIMIZACIONES.md
├── .gitignore
├── .editorconfig
├── global.json
├── Directory.Build.props                    # Analizadores, nullable, advertencias como errores
├── Directory.Packages.props                 # Versiones de paquetes centralizadas
├── WindowsOptimizer.sln
│
├── docs/
│   ├── arquitectura.md
│   ├── amenazas.md                          # Modelo de amenazas del servicio con privilegios
│   ├── reglas-de-analisis.md
│   ├── estimaciones.md                      # Cómo se calculan y calibran las estimaciones
│   └── decisiones/                          # ADRs (registro de decisiones)
│
├── design/                                  # Prototipos de interfaz (exportaciones de Figma)
│
├── src/
│   ├── WindowsOptimizer.App/                # UI WinUI 3
│   │   ├── App.xaml
│   │   ├── Views/                           # Dashboard, Hallazgos, Historial, Disco, Diagnóstico, Ajustes
│   │   ├── ViewModels/
│   │   ├── Controls/                        # Tarjeta de hallazgo, indicador de estimación...
│   │   ├── Converters/
│   │   ├── Services/                        # Navegación, diálogos, temas
│   │   ├── Assets/
│   │   └── Strings/                         # Localización (es, en)
│   │
│   ├── WindowsOptimizer.Core/               # Modelos e interfaces (sin dependencias)
│   │   ├── Models/                          # Finding, ScanResult, ImpactEstimate, ChangeRecord...
│   │   ├── Abstractions/                    # IScanner, IFixAction, IRollbackStore, IRegistryAccess,
│   │   │                                    # IServiceManager, ITaskSchedulerAccess, IImpactEstimator
│   │   └── Enums/                           # Severity, Category, Risk...
│   │
│   ├── WindowsOptimizer.Rules/              # Carga, validación y firma de reglas
│   │   ├── RuleLoader.cs
│   │   ├── RuleValidator.cs
│   │   └── SignatureVerifier.cs
│   │
│   ├── WindowsOptimizer.Platform/           # Acceso real a Windows (implementa las abstracciones de Core)
│   │   ├── Registry/
│   │   ├── Services/
│   │   ├── ScheduledTasks/
│   │   ├── Wmi/
│   │   └── Packages/                        # Apps UWP/MSIX
│   │
│   ├── WindowsOptimizer.Scanners/           # Motor de análisis (solo lectura)
│   │   ├── Services/
│   │   ├── ScheduledTasks/
│   │   ├── Registry/
│   │   ├── Startup/
│   │   ├── TempFiles/
│   │   ├── Apps/
│   │   ├── Network/
│   │   ├── Drivers/
│   │   ├── Privacy/
│   │   ├── Disk/
│   │   ├── Power/
│   │   ├── Hardware/                        # Portátil/sobremesa, SSD/HDD, GPU
│   │   └── ScanOrchestrator.cs
│   │
│   ├── WindowsOptimizer.Measurement/        # Línea base y estimación orientativa
│   │   ├── Baseline/
│   │   ├── Estimators/
│   │   └── BaselineCollector.cs
│   │
│   ├── WindowsOptimizer.Actions/            # Motor de acciones con rollback
│   │   ├── Fixes/
│   │   ├── Rollback/
│   │   ├── History/                         # Historial de cambios y restauración individual
│   │   ├── Drift/                           # Detección de ajustes revertidos
│   │   ├── RestorePointManager.cs
│   │   └── ActionExecutor.cs
│   │
│   ├── WindowsOptimizer.Service/            # Servicio con privilegios (Named Pipes)
│   │   ├── Program.cs
│   │   ├── PipeServer.cs
│   │   └── Handlers/                        # Solo acciones de una lista cerrada
│   │
│   ├── WindowsOptimizer.Cli/                # Modo headless / automatización
│   │
│   ├── WindowsOptimizer.Infrastructure/     # SQLite, logging, configuración
│   │   ├── Data/
│   │   ├── Logging/
│   │   └── Settings/
│   │
│   └── WindowsOptimizer.Native/             # (Opcional) módulo Rust para MFT
│
├── rules/                                   # Reglas declarativas (JSON) validadas por esquema
│   ├── schema/
│   │   └── rule.schema.json
│   ├── presets.json                         # Minimal / Standard / Advanced
│   ├── privacy.json
│   ├── services.json
│   ├── scheduled-tasks.json
│   ├── startup.json
│   ├── apps.json
│   ├── network.json
│   ├── performance.json
│   ├── gaming.json
│   └── cleaners.json
│
├── tests/
│   ├── WindowsOptimizer.Core.Tests/
│   ├── WindowsOptimizer.Rules.Tests/        # Validan que todas las reglas cumplen el esquema
│   ├── WindowsOptimizer.Scanners.Tests/
│   ├── WindowsOptimizer.Measurement.Tests/
│   ├── WindowsOptimizer.Actions.Tests/
│   ├── WindowsOptimizer.Service.Tests/
│   └── lab/                                 # Scripts del laboratorio de VMs (Hyper-V)
│
├── tools/
│   └── RuleValidator/                       # Herramienta de CI que usa WindowsOptimizer.Rules
│
├── backend/                                 # Pospuesto: cuentas y licencias (ver Fase 5)
│   ├── api/
│   │   ├── auth/
│   │   ├── licenses/
│   │   └── devices/
│   ├── db/
│   │   ├── schema.sql
│   │   └── migrations/
│   └── .env.example
│
├── installer/                               # MSIX / WiX, firma
└── .github/
    ├── ISSUE_TEMPLATE/
    └── workflows/                           # CI: build, test, validar reglas, release
```

---

## 4. Roadmap

> Las duraciones no se estiman todavía: se calcularán después del hito "esqueleto vertical" de la Fase 1, cuando se conozca el ritmo real. El alcance V1 (sección 8) es amplio y se revisará en ese momento.

### Fase 0: Entorno, base y decisiones técnicas
- [ ] Instalar Visual Studio (versión vigente) con la carga de trabajo de Windows App SDK y el SDK de la versión LTS de .NET
- [ ] Crear repositorio Git con ramas protegidas y plantillas de issues y PR
- [ ] Crear solución y proyectos según la estructura de la sección 3
- [ ] Configurar `global.json`, `Directory.Build.props` (analizadores, nullable) y `Directory.Packages.props`
- [ ] Configurar inyección de dependencias, logging (Serilog) y CI básico (compilar y probar)
- [ ] Preparar laboratorio de VMs (Hyper-V) con instantáneas: Windows 10 y 11, instalación limpia y "sucia"
- [ ] Decidir el empaquetado (MSIX o sin empaquetar) y cómo convive con el servicio de Windows
- [ ] Redactar el modelo de amenazas del servicio con privilegios (`docs/amenazas.md`)
- [ ] Prototipo de interfaz en Figma de las pantallas clave

### Fase 1: Frontend / Interfaz
- [ ] Shell principal con navegación lateral (NavigationView)
- [ ] Sistema de temas (claro/oscuro) y localización es/en
- [ ] Modo simple (botón único "Analizar") y modo experto
- [ ] Asistente de primera instalación (pregunta cómo se usa el equipo y propone un perfil)
- [ ] Dashboard con puntuación de salud (datos simulados)
- [ ] Lista de hallazgos con filtros por gravedad y categoría, y tarjeta con explicación, riesgo y estimación (rango y confianza)
- [ ] Maqueta de la pantalla de historial
- [ ] Pantallas vacías por módulo
- [ ] Componentes reutilizables (tarjeta de hallazgo, indicador de estimación, barra de progreso)
- [ ] **Hito: esqueleto vertical.** Una regla completa de principio a fin (por ejemplo, desactivar el ID de publicidad): detectar, mostrar, aplicar mediante un servicio mínimo con Named Pipe, y deshacer. Valida la arquitectura antes de construir el resto

### Fase 2: Motor de análisis (solo lectura) y mediciones
**Reglas**
- [ ] Esquema JSON de reglas (sección 6.4) y su validador en CI
- [ ] Cargador de reglas con filtrado por versión de Windows, preset y perfil de equipo
- [ ] Capa `Platform` (registro, servicios, tareas programadas, WMI, paquetes) detrás de interfaces

**Scanners**
- [ ] Definir `IScanner`, `Finding` y `ScanOrchestrator`
- [ ] Archivos temporales y caché
- [ ] Servicios y tareas programadas
- [ ] Claves de registro (privacidad y telemetría)
- [ ] Programas de arranque, con atribución de tiempo
- [ ] Apps preinstaladas
- [ ] Red y DNS (conexiones, DNS actual, latencia)
- [ ] Drivers (estado y antigüedad)
- [ ] Energía y hardware (portátil/sobremesa, SSD/HDD, GPU)
- [ ] Analizador de disco visual (tipo TreeSize)
- [ ] Diagnóstico "¿por qué va lento ahora?"

**Mediciones**
- [ ] Línea base: tiempo de arranque, RAM en reposo, procesos al inicio y espacio libre
- [ ] Estimadores por tipo de regla, con nivel de confianza (sección 8.2)
- [ ] Conectar resultados reales a la UI
- [ ] Pruebas unitarias de scanners y estimadores

### Fase 3: Motor de acciones, rollback e historial
- [ ] Servicio con privilegios completo: Named Pipe con permisos restringidos y solo acciones de lista cerrada
- [ ] Punto de restauración antes de aplicar cambios (gestionar el caso en que Windows limite su creación)
- [ ] Almacén de rollback (valores anteriores en SQLite)
- [ ] Acciones: limpieza, DNS, servicios, tareas, arranque, telemetría y apps
- [ ] Historial de cambios con restauración individual y total
- [ ] Detección de deriva (ajustes revertidos por Windows Update)
- [ ] Modo juego básico (plan de energía y servicios)
- [ ] Nueva medición tras aplicar, guardada en el historial para calibrar estimaciones
- [ ] Pruebas en VM: "aplicar todo / deshacer todo" y efectos secundarios (red local, impresoras, Windows Update)

### Fase 4: Pulido y lanzamiento
- [ ] Rendimiento y consumo de memoria de la propia app
- [ ] Validar las estimaciones en varias máquinas (HDD/SSD, equipos antiguos y nuevos)
- [ ] Autoactualización y canales estable/beta
- [ ] Empaquetado MSIX/instalador y firma de código
- [ ] Telemetría propia opcional y con consentimiento
- [ ] Documentación de usuario
- [ ] Beta cerrada, recogida de feedback y corrección de errores
- [ ] Lanzamiento v1.0

### Fase 5 (pospuesta): Negocio, cuentas y licencias
> Se decidirá al final junto con el modelo de negocio. Si el producto es de pago, esta fase debe completarse antes del lanzamiento público.

- [ ] Nombre definitivo, público objetivo y modelo de negocio
- [ ] Aspectos legales: EULA, política de privacidad y RGPD
- [ ] Esquema PostgreSQL en Neon (usuarios, dispositivos, licencias)
- [ ] API de autenticación con JWT
- [ ] Vinculación de dispositivos (hasta 3, MAC hasheada)
- [ ] Tipos de cuenta y expiración con fechas server-side
- [ ] Integración en la app (login, estado de licencia)
- [ ] Revisión de seguridad (límite de peticiones, validaciones)
- [ ] Página web y página de descarga

### Después de v1.0
Las funciones marcadas V2 y Futuro de la sección 8 se planifican una vez publicada la versión 1.0.

---

## 5. Riesgos y consideraciones

| Riesgo | Mitigación |
|---|---|
| Un cambio rompe el sistema del usuario | Punto de restauración, rollback, modo solo análisis y pruebas en VM |
| Efectos secundarios no previstos (red local, impresoras, actualizaciones) | Batería de pruebas de efectos secundarios tras cada perfil |
| Falsos positivos en antivirus | Firma de código, sin ofuscación agresiva, envío de muestras a los fabricantes |
| Cambios de Windows entre versiones | Reglas versionadas con compatibilidad por compilación y pruebas en varias versiones |
| Elevación de privilegios insegura | UI sin privilegios, servicio que solo ejecuta una lista cerrada de acciones, permisos estrictos en la Named Pipe |
| Reglas manipuladas | Validación por esquema y firma de las reglas antes de ejecutarlas |
| Estimaciones de mejora que no se cumplen | Rangos prudentes, nivel de confianza, "impacto no estimable" y validación en varias máquinas |
| Windows revierte los ajustes tras una actualización | Detección de deriva y opción de reaplicar |
| Manipulación de licencias (fase pospuesta) | Validación server-side, fechas con `now()`, JWT firmado |

---

## 6. Análisis de proyectos de referencia: mejores prácticas

> Revisados: README, `SPEC.md` y documentación pública de cada repositorio (no el código fuente completo). Para profundizar, conviene clonar cada repo y leer el código de los módulos que más nos interesen. **Antes de reutilizar nada, comprobar la licencia** (WinUtil es MIT; el fichero `winapp2.ini` no tiene licencia explícita).

### 6.1 Resumen de los proyectos

| Proyecto | Tipo | Qué aporta como referencia |
|---|---|---|
| [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) | Script PowerShell + interfaz WPF | Config declarativa en JSON, presets, undo, pruebas y CI |
| [hellzerg/optimizer](https://github.com/hellzerg/optimizer) | App C# de escritorio | Catálogo funcional muy completo, automatización por CLI, multilenguaje |
| [hellzerg/optimizerNXT](https://github.com/hellzerg/optimizerNXT) | CLI dirigida por YAML | Configuraciones firmadas, operaciones condicionales |
| [undergroundwires/privacy.sexy](https://github.com/undergroundwires/privacy.sexy) | App multiplataforma | Arquitectura data-driven, scripts reversibles, transparencia |
| [farag2/Sophia-Script-for-Windows](https://github.com/farag2/Sophia-Script-for-Windows) | Módulo PowerShell | Versiones por edición de Windows, releases automáticas |
| [BleachBit](https://docs.bleachbit.org/cml/cleanerml.html) + [winapp2.ini](https://github.com/bleachbit/winapp2.ini) | Limpiador de sistema | Reglas de limpieza como datos (CleanerML / winapp2) |

### 6.2 Lo que hace cada uno y qué aprendemos

#### WinUtil (Chris Titus Tech)
**Cómo lo hace**
- El repositorio es modular (`functions/`, `config/`, `xaml/`, `scripts/`, `pester/`, `lint/`, `docs/`), pero el entregable es un único `winutil.ps1` **generado** por `Compile.ps1`. El fichero compilado no se edita a mano.
- Las listas de aplicaciones, tweaks, presets, proveedores DNS y navegación viven en **JSON** (`config/*.json`), no en código.
- En `tweaks.json`, los cambios de registro y servicios deben incluir **valores originales** para poder deshacerlos.
- Hay **presets** (`Standard`, `Minimal`, `Advanced`) y un modo automatizado por parámetro.
- La UI usa convenciones de nombres: un control `WPFThingButton` se enlaza con una función `Invoke-WPFThingButton`.
- Las tareas largas usan runspaces para no bloquear la UI, y las actualizaciones de interfaz se despachan al hilo de UI.
- Calidad: pruebas **Pester**, análisis estático con **PSScriptAnalyzer** y comprobación de compilación en GitHub Actions.
- Los flujos de ISO trabajan sobre copias y nunca modifican la ISO original del usuario.
- El propio README menciona una implementación .NET "más rápida" de pago, lo que refuerza la elección de C#/.NET frente a un script.

**Qué copiamos**
- Reglas declarativas con **metadatos de undo obligatorios**.
- Presets (Minimal / Standard / Advanced).
- Convención de nombres que enlaza vista, comando y regla.
- Pruebas automáticas que validan los ficheros de reglas.
- Análisis estático y CI desde el primer día.

#### Optimizer (hellzerg)
**Cómo lo hace**
- Aplicación C# portable con un catálogo amplio: servicios, telemetría, limpieza de disco y navegadores, programas de inicio, edición del `hosts`, cambio de DNS desde una lista predefinida, vaciado de caché DNS, ping/latencia, inspección de hardware y detección de archivos bloqueados.
- Soporta unos 24 idiomas.
- Tiene guía de **automatización** y opciones de **línea de comandos**.
- Recomienda usarlo tras una instalación limpia de Windows.

**Qué copiamos**
- Soporte de **CLI / modo silencioso** además de la UI (útil para técnicos y pruebas).
- Localización desde el principio (archivos de recursos, sin textos fijos en código).
- Herramientas "pequeñas pero útiles" dentro de la app (DNS, hosts, latencia, archivos bloqueados).

#### OptimizerNXT (hellzerg)
**Cómo lo hace**
- Todo se define en **YAML**: registro, DNS, servicios, procesos, inicio, hosts, UWP y scripts.
- Admite **operaciones condicionales** según estado del sistema, usuario o entorno.
- Solo ejecuta YAML **firmado** con un certificado de confianza.

**Qué copiamos**
- **Firmar las reglas** (o su paquete) y verificar la firma antes de ejecutarlas. Si las reglas se descargan de un servidor, esto evita que alguien las manipule.
- Condiciones en las reglas (versión de Windows, edición, tipo de equipo).

#### privacy.sexy
**Cómo lo hace**
- Aplicación **data-driven**: la lógica específica de cada sistema operativo se lee de ficheros YAML (colecciones), con funciones reutilizables y un lenguaje de plantillas.
- Principios declarados: transparente, **reversible**, abierto y probado (tests automáticos y manuales).
- Cada script se documenta y debería incluir su forma de revertirlo.
- Niveles de recomendación **standard** y **strict**.
- Incluye notificación de nuevas versiones y documentación de arquitectura (`architecture.md`, `collection-files.md`, `templating.md`).
- Un issue público sobre "revertir todo" muestra que revertir en bloque puede dar comportamientos inesperados si no se prueba.

**Qué copiamos**
- Separar **motor** (código) de **conocimiento** (reglas en datos).
- Mostrar al usuario exactamente qué hará cada cambio **antes** de activarlo.
- Niveles de riesgo en cada regla y marcar de forma explícita las que **no son reversibles**.
- Documentar la arquitectura en `docs/` desde el principio.
- Probar el flujo completo "aplicar todo / deshacer todo" en una VM limpia.

#### Sophia Script
**Cómo lo hace**
- Módulo PowerShell con **ediciones separadas** para Windows 10, 11, LTSC y PowerShell 7.
- Los paquetes se generan automáticamente con GitHub Actions.
- Un issue antiguo mostró un efecto secundario real (tras ejecutar el script dejó de funcionar el acceso a carpetas compartidas por una regla de firewall) y se corrigió añadiendo una regla.

**Qué copiamos**
- Cada regla declara **para qué versiones de Windows** es válida; la app oculta o desactiva las que no aplican.
- Lanzamientos 100 % automatizados por CI.
- Pruebas de **efectos secundarios** (red local, impresoras, carpetas compartidas, Windows Update) tras aplicar cada perfil.

#### BleachBit + winapp2.ini
**Cómo lo hace**
- Las reglas de limpieza son **datos** (CleanerML en XML, o formato winapp2.ini con más de 2.400 reglas de la comunidad).
- Detección de sistema operativo en tiempo de ejecución para descartar reglas que no aplican, y expansión de variables de entorno.
- Combina escaneos profundos del mismo directorio para mejorar el rendimiento.
- Puede limpiar entradas concretas de ficheros `.ini`, JSON y bases SQLite sin borrar el archivo entero.
- El repositorio de winapp2 incluye scripts de **validación** (`check_ini.py`) para revisar cada cambio.

**Qué copiamos**
- Reglas de limpieza como datos, con detección ("solo si existe esta ruta o clave").
- Un **validador** de reglas que se ejecuta en CI.
- Agrupar escaneos del mismo directorio para no recorrerlo varias veces.

### 6.3 Patrones comunes (lo que hacen casi todos)

1. **Datos, no código**: las reglas viven en JSON/YAML/XML y el motor las interpreta.
2. **Reversibilidad**: se guarda el estado original y hay opción de deshacer.
3. **Presets por nivel de riesgo**: de lo seguro a lo agresivo.
4. **Transparencia**: el usuario ve qué hace cada cambio.
5. **Automatización**: CLI, parámetros o perfiles guardados.
6. **Calidad automatizada**: pruebas, análisis estático y releases por CI.
7. **Compatibilidad por versión de Windows** declarada en cada regla.

### 6.4 Esquema de regla propuesto para nuestro proyecto

```json
{
  "id": "privacy.disable-advertising-id",
  "name": "Desactivar ID de publicidad",
  "category": "privacy",
  "risk": "low",
  "presets": ["minimal", "standard", "advanced"],
  "windows": { "min_build": 17763, "editions": ["Home", "Pro", "Enterprise"] },
  "detect": {
    "registry": {
      "path": "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",
      "name": "Enabled",
      "bad_value": 1
    }
  },
  "apply": [
    { "type": "registry_set", "path": "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo", "name": "Enabled", "value": 0, "kind": "DWord" }
  ],
  "undo": "auto_from_snapshot",
  "reversible": true,
  "requires_admin": false,
  "requires_reboot": false,
  "explain": {
    "what": "Qué es.",
    "why": "Por qué importa.",
    "changes": "Qué cambia exactamente."
  },
  "impact": {
    "type": "privacy",
    "estimator": "none",
    "confidence": "high",
    "show_percentage": false
  },
  "sources": ["WinUtil: tweak de telemetría (documentación)"]
}
```

Campos clave:
- `detect`: la regla alimenta el análisis.
- `apply` y `undo`: la regla alimenta el motor de acciones. `undo: "auto_from_snapshot"` restaura el valor guardado antes del cambio, o borra la clave si no existía (como el `<RemoveEntry>` de WinUtil).
- `risk` y `presets`: alimentan la UI y los perfiles.
- `windows`: compatibilidad por versión y edición.
- `explain`: texto que ve el usuario (qué es, por qué importa, qué cambia).
- `impact`: cómo se estima la mejora (ver 8.2). Tipos previstos: `boot_time`, `ram`, `disk_space`, `latency`, `privacy` y `none`; confianza `high`, `medium` o `low`.

### 6.5 Decisiones que se derivan para nuestro proyecto

| Decisión | Motivo |
|---|---|
| Las reglas se guardan en `rules/*.json` con **esquema JSON** y se validan en CI | Evita reglas rotas (WinUtil, winapp2) |
| `undo` obligatorio salvo `reversible: false`, que se muestra con aviso en la UI | WinUtil, privacy.sexy |
| Presets Minimal / Standard / Advanced | WinUtil, privacy.sexy |
| El análisis reutiliza el campo `detect` de las reglas | Un solo origen de verdad |
| Reglas firmadas si se descargan desde servidor | OptimizerNXT |
| CLI headless (`WindowsOptimizer.Cli`) además de la UI | Optimizer |
| Compatibilidad por build de Windows en cada regla | Sophia Script |
| Batería de pruebas de efectos secundarios en VM | Incidente de Sophia con carpetas compartidas |
| Documentación de arquitectura desde el día 1 | privacy.sexy |

### 6.6 Siguientes pasos de investigación

- [x] Clonar WinUtil y leer `config/tweaks.json` completo, `config/preset.json` y `functions/` para ver el formato real de undo (hasta ahora solo se han revisado los tweaks de telemetría y servicios a través de su documentación).
- [x] Leer `architecture.md` y `collection-files.md` de privacy.sexy.
- [x] Revisar el código de Optimizer (módulos de limpieza, servicios y arranque).
- [x] Estudiar el formato CleanerML/winapp2 para nuestras reglas de limpieza.
- [x] Analizar un analizador de disco abierto (por ejemplo WinDirStat) para el módulo de peso de archivos.
- [x] Revisar licencias de todo lo que queramos reutilizar o inspirar de forma directa.

---

## 7. Fuentes y referencias

> **Regla del proyecto:** cuando necesitemos aprender cómo se hace algo (formato de reglas, undo, limpieza, servicios, red, CI, empaquetado...), **primero revisamos estas fuentes** antes de inventar una solución. Leer cómo lo resuelven proyectos maduros ahorra tiempo y errores. Antes de reutilizar código o reglas, comprobar siempre la licencia.

### 7.1 Repositorios de referencia (de donde sale el análisis)

| Proyecto | Enlace | Consultar para |
|---|---|---|
| WinUtil (Chris Titus Tech) | https://github.com/ChrisTitusTech/winutil | Config JSON (`config/tweaks.json`, `preset.json`), presets, undo, pruebas Pester, CI, `SPEC.md` y `CONTRIBUTING.md` |
| Documentación de WinUtil | https://winutil.christitus.com/ | Cómo se usan y desarrollan los tweaks, problemas conocidos |
| Optimizer (hellzerg) | https://github.com/hellzerg/optimizer | App C#: limpieza, servicios, inicio, hosts, DNS, automatización y opciones de línea de comandos |
| OptimizerNXT (hellzerg) | https://github.com/hellzerg/optimizerNXT | Configuración YAML, YAML firmado, operaciones condicionales |
| privacy.sexy | https://github.com/undergroundwires/privacy.sexy | Arquitectura data-driven (`architecture.md`, `collection-files.md`, `templating.md`), scripts reversibles, niveles standard/strict |
| Sophia Script for Windows | https://github.com/farag2/Sophia-Script-for-Windows | Ediciones por versión de Windows, releases con GitHub Actions, historial de incidencias con efectos secundarios |
| BleachBit | https://docs.bleachbit.org/cml/cleanerml.html | Formato CleanerML, detección de SO, variables, rendimiento de escaneo |
| winapp2.ini (BleachBit) | https://github.com/bleachbit/winapp2.ini | Reglas de limpieza de la comunidad y scripts de validación (`check_ini.py`) |

### 7.2 Dónde mirar según el tema

| Si queremos aprender... | Revisar primero |
|---|---|
| Formato de reglas y esquema | WinUtil (`config/`), privacy.sexy (`collection-files.md`), CleanerML |
| Undo / rollback de cambios | WinUtil (`tweaks.json` y `SPEC.md`), privacy.sexy (scripts de revertir) |
| Presets y niveles de riesgo | WinUtil (`preset.json`), privacy.sexy (standard/strict) |
| Limpieza de temporales y caché | BleachBit, winapp2.ini, Optimizer |
| Servicios, arranque y telemetría | WinUtil, Optimizer, Sophia Script |
| Red y DNS | WinUtil (proveedores DNS), Optimizer (cambio de DNS, ping, flush) |
| Firmar y proteger reglas | OptimizerNXT |
| Compatibilidad por versión de Windows | Sophia Script |
| Pruebas, lint y CI | WinUtil (`pester/`, `lint/`, `.github/`), Sophia Script (workflows), winapp2.ini (validación) |
| Documentar la arquitectura | privacy.sexy (`architecture.md`) |
| Automatización / CLI | Optimizer (opciones de línea de comandos y guía de automatización) |

### 7.3 Documentación oficial (Microsoft y herramientas)

Para APIs y comportamiento del sistema, la fuente de verdad es la documentación oficial.

> Los enlaces de esta lista están escritos de memoria y no se han comprobado; verificar que abren antes de depender de ellos.

- WinUI 3 / Windows App SDK: https://learn.microsoft.com/windows/apps/winui/
- Documentación de .NET: https://learn.microsoft.com/dotnet/
- Empaquetado MSIX: https://learn.microsoft.com/windows/msix/
- WMI / CIM: https://learn.microsoft.com/windows/win32/wmisdk/
- CommunityToolkit.Mvvm: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/
- Neon (PostgreSQL): https://neon.com/docs
- Serilog: https://serilog.net/
- GitHub Actions: https://docs.github.com/actions

### 7.4 Cómo usar estas fuentes

1. **Buscar primero** en el repositorio de referencia más cercano al tema (tabla 7.2).
2. **Leer el código y la configuración reales**, no solo el README.
3. **Comprobar la licencia** antes de copiar nada; inspirarse en el enfoque es distinto de reutilizar código.
4. **Probar en una VM** cualquier técnica nueva antes de añadirla como regla.
5. **Anotar la fuente** en el campo `sources` de cada regla y en `docs/decisiones/` cuando una decisión se base en uno de estos proyectos.

### 7.5 Documentos relacionados

- `CATALOGO_OPTIMIZACIONES.md`: lista de servicios, tareas programadas, claves de registro, apps y carpetas que revisan los proyectos de referencia, y qué debería analizar nuestro programa. Cada dato indica su fuente y su nivel de riesgo.

---

## 8. Funciones adicionales e ideas

> Prioridad: **V1** = versión 1.0 · **V2** = después del lanzamiento · **Futuro** = ambicioso o dependiente de otras decisiones.

### 8.1 Funciones diferenciadoras (V1)

| Función | Descripción | Prioridad |
|---|---|---|
| **Estimación orientativa de mejora** | Antes de aplicar un cambio, la app informa de la mejora aproximada que se espera (ver 8.2) | V1 |
| **Explicación de cada hallazgo** | Qué es, por qué importa, qué cambia y cómo deshacerlo; con "modo aprendizaje" opcional | V1 |
| **Historial de cambios con restauración individual** | Línea de tiempo visual de todo lo aplicado; se puede revertir un cambio concreto, no solo todo | V1 |
| **Análisis del arranque con atribución** | Qué programa o servicio añade cuántos segundos al inicio, usando los registros de rendimiento de Windows | V1 |
| **Detección de deriva (drift)** | Detecta ajustes que Windows Update ha revertido y ofrece reaplicarlos | V1 |
| **Perfiles de hardware** | Detecta portátil/sobremesa, SSD/HDD y GPU dedicada, y adapta las recomendaciones (distintos de los presets de riesgo Minimal/Standard/Advanced) | V1 |

### 8.2 Estimación orientativa de mejora (diseño)

**Objetivo:** que el usuario sepa, **antes de aplicar** un cambio, cuánto podría ganar aproximadamente. Por ejemplo: *"Desactivar estos 6 programas de inicio podría reducir el arranque en torno a un 10-20 %"*.

**Principios**
- Es una **estimación orientativa**, nunca una promesa. La interfaz lo indica siempre ("aprox.", "estimado").
- Se muestra como **rango** o franja cualitativa (bajo / medio / alto), no como un número exacto que aparente precisión.
- Se calcula a partir de **datos reales del propio equipo**, no de porcentajes fijos universales.
- Si no hay datos suficientes para estimar con honestidad, la app muestra **"impacto no estimable"** en lugar de inventar una cifra.
- Los cambios de privacidad o seguridad no se presentan como mejora de velocidad; llevan su propia etiqueta (por ejemplo "reduce el envío de datos").

**Cómo estimar según el tipo de regla**

| Tipo de hallazgo | Base de la estimación |
|---|---|
| Programas de inicio | Tiempo de arranque medido y contribución de cada programa al inicio |
| Espacio en disco | Tamaño real de los archivos detectados (esta cifra sí es exacta) |
| Servicios innecesarios | Memoria y CPU que consume cada servicio en el momento del análisis |
| Procesos en segundo plano | Uso de RAM/CPU medido |
| Red y DNS | Latencia medida frente a los DNS alternativos |
| Tweaks de registro sin efecto medible | "Impacto no estimable" (no se muestra porcentaje) |

**Dónde se define:** el campo `impact` de cada regla (ver esquema en 6.4) indica el tipo de impacto, el estimador, la confianza y si se muestra porcentaje.

**Interfaz**
- Cada hallazgo muestra su estimación junto al nivel de confianza.
- En el resumen previo a aplicar: *"Mejora estimada total: aprox. X-Y %"*, con el aviso "los resultados reales pueden variar".
- Tras aplicar, la app puede **medir de nuevo** y guardar el resultado en el historial. Sirve para el usuario y para **calibrar internamente las estimaciones** con el tiempo.

**Riesgos a vigilar**
- Una cifra optimista que no se cumple daña la confianza en el producto. Por eso se prefieren rangos prudentes.
- Hay que probar las estimaciones en varias máquinas (HDD/SSD, equipos antiguos y nuevos) antes de publicarlas.

### 8.3 Diagnóstico

| Función | Descripción | Prioridad |
|---|---|---|
| "¿Por qué va lento ahora?" | Instantánea de procesos con más CPU, disco y red, con explicación y acciones | V1 |
| Salud del disco | Estado SMART, desgaste de SSD y espacio libre con proyección | V2 |
| Salud de la batería | Capacidad de diseño frente a capacidad actual (portátiles) | V2 |
| Temperaturas y throttling | Detectar reducción de frecuencia por calor en CPU/GPU | V2 |
| Informe exportable | PDF o HTML del estado del equipo, útil para soporte técnico | V2 |

### 8.4 Rendimiento y mantenimiento

| Función | Descripción | Prioridad |
|---|---|---|
| Analizador de disco visual | Mapa de árbol tipo TreeSize, con detección de duplicados y archivos antiguos grandes | V1 |
| Gestor de programas instalados | Tamaño, fecha de instalación y último uso; sugiere los que no se usan | V2 |
| Limpieza de caché de desarrollo | `node_modules`, caché de NuGet, etc., útil si el público incluye programadores | V2 |
| Revisión de actualizaciones de drivers | Compara versiones instaladas con las publicadas por el fabricante; solo fuentes oficiales | V2 |
| Mantenimiento programado | En segundo plano, solo cuando el equipo está inactivo, con notificaciones discretas | V2 |

### 8.5 Gaming

| Función | Descripción | Prioridad |
|---|---|---|
| Modo juego básico | Plan de energía y servicios en segundo plano, activable a mano | V1 |
| Modo juego con perfil por juego | Al detectar un ejecutable aplica un perfil (energía, procesos a cerrar, pausar actualizaciones) y lo revierte al cerrarlo | V2 |
| Comprobaciones específicas | Modo juego de Windows, GPU de alto rendimiento por app, optimizaciones de pantalla completa, planificación de GPU acelerada | V2 |
| Latencia a servidores de juegos | Informe de ping a servidores populares | V2 |

### 8.6 Privacidad y seguridad

| Función | Descripción | Prioridad |
|---|---|---|
| Auditoría de privacidad | Qué apps tienen acceso a cámara, micrófono y ubicación, con opción de revocarlo | V2 |
| Revisión de configuración segura | Estado de Defender, firewall, BitLocker, SMB1 y escritorio remoto; solo informa, no cambia sin permiso | V2 |
| Revisor de puertos y conexiones | Explica cada proceso que se conecta al exterior | V2 |
| Revisión de `hosts`, proxy y DNS | Detecta modificaciones inesperadas (posible malware) | V2 |

### 8.7 Experiencia de usuario

| Función | Descripción | Prioridad |
|---|---|---|
| Modo simple y modo experto | Botón único "Analizar" frente a panel completo de reglas | V1 |
| Bandeja del sistema | Estado rápido y acciones frecuentes | V2 |
| Plantillas compartibles | Exportar/importar configuración como archivo | V2 |
| Modo técnico / CLI | Automatización en varios equipos (inspirado en Optimizer) | V2 |
| Asistente de primera instalación | Pregunta cómo se usa el equipo y propone un perfil inicial | V1 |

### 8.8 Ideas ambiciosas (Futuro)

- **Preparador de instalación limpia de Windows:** lista de verificación posterior a la instalación (drivers, ajustes y apps).
- **Comunidad de reglas:** reglas aportadas por la comunidad, con revisión y firma, al estilo de winapp2.ini.
- **Asistente con IA** que explique hallazgos en lenguaje natural. Debe ser opcional o local para respetar la privacidad.
- **Aplicación móvil acompañante** para consultar informes.

### 8.9 Dónde encaja en el roadmap

Las funciones **V1** están integradas en las fases 1 a 4 de la sección 4. Las funciones **V2** y **Futuro** se planifican después de publicar la versión 1.0. El alcance V1 es amplio: tras el hito del esqueleto vertical se revisará y se moverá a una versión 1.1 lo que no quepa.
