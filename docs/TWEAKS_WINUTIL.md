# WinUtil Tweaks (Roadmap de Implementación)

Este documento contiene la lista de mejoras extraídas de la herramienta WinUtil (de Chris Titus), categorizadas según su nivel de riesgo e impacto para integrarlas paulatinamente en nuestro motor de `WindowsOptimizer`.

## 🟢 Essential Tweaks (Riesgo Bajo - Perfil 'Standard')
Estos ajustes son seguros para la gran mayoría de usuarios y forman la base del perfil Standard.

- [ ] Activity History - Disable
- [ ] BitLocker - Disable *(Nota: A evaluar, desactivar cifrado puede ser polémico)*
- [ ] ConsumerFeatures - Disable (Bloatware sugerido)
- [ ] Delivery Optimization - Disable
- [ ] Disk Cleanup - Run (Integrado en nuestro módulo de limpiadores)
- [ ] End Task With Right Click - Enable (Windows 11)
- [ ] File Explorer Automatic Folder Discovery - Disable
- [ ] Hibernation - Disable (Ahorra espacio en disco)
- [ ] Location Tracking - Disable
- [ ] Microsoft Store Recommended Search Results - Disable
- [ ] Prevent Device Companion Apps
- [ ] Restore Point - Create (Lo manejaremos globalmente en Configuración)
- [ ] Services - Set to Manual (Optimización de servicios)
- [ ] Start Menu Previous Layout - Enable
- [ ] Telemetry - Disable
- [ ] Temporary Files - Remove (Módulo de limpiadores)
- [ ] Widgets - Remove
- [ ] Windows Platform Binary Table (WPBT) - Disable

## 🟡 Customize Preferences (Riesgo Bajo/UI - Perfil 'Standard' o 'Minimal')
Cambios de interfaz de usuario (UI/UX) o preferencias personales.

- [ ] BSoD Verbose Mode
- [ ] Dark Theme for Windows
- [ ] Enable Long Paths (Más de 260 caracteres)
- [ ] File Explorer File Extensions (Mostrar extensiones de archivo)
- [ ] File Explorer Hidden Files (Mostrar archivos ocultos)
- [ ] Game Mode (Activar modo juego)
- [ ] Lock Screen - Disable
- [ ] Logon Screen Acrylic Blur - Disable
- [ ] Logon Verbose Mode
- [ ] Microsoft Outlook New Version (Desactivar/Remover)
- [ ] Mouse Acceleration (Desactivar)
- [ ] Num Lock on Startup (Activar)
- [ ] S0 Sleep Network Connectivity (Desactivar)
- [ ] S3 Sleep (Activar)
- [ ] Scrollbars Always Visible
- [ ] Settings Home Page (Ocultar recomendaciones)
- [ ] Start Menu Bing Search (Desactivar)
- [ ] Start Menu Recommendations (Desactivar)
- [ ] Sticky Keys (Desactivar accesibilidad accidental)
- [ ] System Tray Battery Percentage
- [ ] Taskbar Centered Icons (Alineación a la izquierda o centro)
- [ ] Taskbar Search Icon (Ocultar/Compactar)
- [ ] Taskbar Task View Icon (Ocultar)
- [ ] Window Snapping

## 🔴 Advanced Tweaks - CAUTION (Riesgo Alto - Perfil 'Advanced')
Ajustes que pueden romper ciertas dependencias o funciones requeridas por los usuarios promedio.

- [ ] Adobe URL Block List - Enable
- [ ] Background Apps - Disable (Puede romper notificaciones de apps UWP)
- [ ] Brave Browser - Debloat
- [ ] Date & Time - Set Time to UTC (Útil para dual-boot con Linux)
- [ ] Disable Reserved Storage
- [ ] File Explorer Home and Gallery - Disable
- [ ] IPv6 - Disable
- [ ] IPv6 - Set IPv4 as Preferred
- [ ] Logitech Download Assistant Auto-Install - Disable
- [ ] Microsoft Edge - Debloat
- [ ] Microsoft Edge - Remove (Muy agresivo, puede romper búsquedas)
- [ ] Microsoft OneDrive - Remove
- [ ] Razer Software Auto-Install - Disable
- [ ] RDP Unsigned File Warnings - Disable
- [ ] Right-Click Menu Previous Layout - Enable (Volver al menú viejo de Win10)
- [ ] Storage Sense - Disable
- [ ] System Tray Notifications & Calendar - Disable
- [ ] Teredo - Disable
- [ ] Visual Effects - Set to Best Performance (Desactiva animaciones de Windows)
- [ ] Windows AI - Disable And Remove (Copilot, Recall)
- [ ] O&O ShutUp10++ - Run
- [ ] DNS - Set to: Default (Otras opciones como Quad9, Cloudflare, AdGuard)

## 🟣 Performance Plans (Riesgo Medio - Solo Escritorio)
- [ ] Ultimate Performance Profile - Enable/Disable
- [ ] Multiplane Overlay (MPO) - Enable/Disable
