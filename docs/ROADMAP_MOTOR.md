# Roadmap y Arquitectura Técnica del Motor de Reglas y Limpieza

Este documento detalla cómo vamos a construir el núcleo de **WindowsOptimizer**, fusionando las mejores técnicas aprendidas de los líderes del sector (WinUtil, privacy.sexy, Optimizer, BleachBit/winapp2) en un único motor C# moderno, rápido y seguro.

---

## 1. Módulo de Limpieza (Archivos Temporales y Caché)

### 🧐 Cómo lo hacen los demás
- **Optimizer (hellzerg)**: Enfoque **imperativo** (C# duro). Tiene rutas hardcodeadas en clases estáticas (ej. `AppData\Local\Google\Chrome\User Data`) y métodos fijos. Es rápido, pero rígido. Para añadir un nuevo programa a limpiar, hay que recompilar la app.
- **BleachBit / winapp2.ini**: Enfoque **declarativo**. Usan variables mágicas (`%LocalAppData%`) y filtros (ej. `*|RECURSE`). La comunidad puede añadir reglas fácilmente modificando un archivo de texto sin tocar el código base.

### 🚀 Cómo lo haremos nosotros
Usaremos el **enfoque declarativo de winapp2** pero serializado en **JSON** para integrarse nativamente con C# moderno (System.Text.Json).
- **Variables Mágicas**: Nuestro motor de C# parseará variables de entorno como `%LocalAppData%`, `%Temp%`, `%SystemDrive%`, `%WinDir%`.
- **Expansión de Rutas**: Implementaremos modificadores como `|RECURSE` (para borrar subcarpetas) y soporte para wildcards `*` (ej. `Chrome\User Data\*\Cache`).
- **Motor C#**: Leerá el JSON y ejecutará la expansión usando las APIs nativas de Win32 o `System.IO.Enumeration` para máximo rendimiento (superando a `DirectoryInfo.GetFiles`).

**Ejemplo de cómo será nuestra regla JSON:**
```json
{
  "id": "CleanChromeCache",
  "name": "Caché de Google Chrome",
  "category": "Limpieza de Navegadores",
  "detect": {
    "type": "FolderExists",
    "path": "%LocalAppData%\\Google\\Chrome\\User Data"
  },
  "actions": [
    {
      "type": "DeleteFiles",
      "path": "%LocalAppData%\\Google\\Chrome\\User Data\\*\\Cache\\*|RECURSE"
    }
  ],
  "presets": ["Standard", "Advanced"]
}
```

---

## 2. Sistema de Reversión (Undo / Rollback)

### 🧐 Cómo lo hacen los demás
- **WinUtil**: Usa un campo `OriginalValue`. Si la clave de registro no existía, usa la constante mágica `<RemoveEntry>`. Para scripts, usan `UndoScript`.
- **privacy.sexy**: Exige programar un `revertCode` explícito junto al `code`.

### 🚀 Cómo lo haremos nosotros
Adoptaremos **la seguridad explícita de privacy.sexy y la elegancia de `<RemoveEntry>` de WinUtil**.
- Cada regla que modifique el sistema (Registry, Servicios, Tareas) DEBE tener su contraparte de reversión o ser marcada explícitamente como `reversible: false`.
- **Registry**: El motor detectará el estado actual antes de aplicar. Si la clave no existía, la acción `Undo` generada automáticamente será eliminar la clave (equivalente a `<RemoveEntry>`).
- **Servicios**: Si desactivamos un servicio (`Start=4`), el `Undo` restaurará el valor original (ej. `Start=2` automático).

**Ejemplo de regla de Registro:**
```json
{
  "id": "DisableTelemetry",
  "actions": [
    {
      "type": "Registry",
      "path": "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
      "name": "AllowTelemetry",
      "value": "0",
      "valueType": "DWord",
      "undo": "<RemoveEntry>" // O "3" si el estándar de Windows es 3
    }
  ]
}
```

---

## 3. Presets y Perfiles de Riesgo

### 🧐 Cómo lo hacen los demás
- **WinUtil**: Un archivo `preset.json` que mapea cada nivel (Standard, Minimal) a un array gigantesco de IDs de reglas.
- **privacy.sexy**: Las reglas mismas dicen si son `standard` o `strict`.

### 🚀 Cómo lo haremos nosotros
Adoptaremos el **enfoque descentralizado de privacy.sexy**. Cada regla en su archivo JSON declarará a qué perfiles pertenece (`presets: ["Minimal", "Standard", "Advanced"]`).
- **Ventaja**: Al añadir una nueva regla, no hay que buscar y actualizar un archivo central `preset.json`. La regla se autodefine.
- El ViewModel que ya construimos (`DashboardViewModel`) simplemente filtrará en memoria las reglas que contengan el `CurrentPreset`.

---

## 4. Arquitectura Data-Driven y CI/CD

### 🧐 Cómo lo hacen los demás
- **privacy.sexy**: Diseño guiado por el dominio (DDD). Interfaz gráfica totalmente separada de la lógica de los scripts.
- **WinUtil / winapp2**: Tienen scripts de validación (`lint/`, `pester/`, `check_ini.py`) que corren en GitHub Actions para asegurar que un contribuyente no rompa las reglas.

### 🚀 Cómo lo haremos nosotros
- **Single Source of Truth**: Los archivos JSON en la carpeta `Rules/` serán la única fuente de la verdad. La UI (WinUI 3) se construirá dinámicamente leyendo el JSON.
- **JSON Schema**: Crearemos un archivo `schema.json` que defina qué campos son obligatorios.
- **CI/CD Integrado**: GitHub Actions validará automáticamente cada regla nueva contra el esquema JSON. Si alguien hace una regla sin `undo` y no marca `reversible: false`, el PR fallará.

---

## Siguientes Pasos (Ejecución)

1. **Crear el Esquema Base de C#**: Definir las clases `Rule`, `Action`, `Condition` para deserializar el JSON.
2. **Implementar el `RuleEngine`**: El cerebro que lee la lista de reglas y evalúa las condiciones de detección.
3. **Desarrollar el `VariableExpander`**: La utilidad que convierte `%LocalAppData%` en `C:\Users\...\AppData\Local` y maneja los comodines `*` y modificadores `|RECURSE`.
4. **Migrar Reglas Falsas**: Convertir nuestras simulaciones actuales (Telemetría, etc.) a archivos JSON reales.
