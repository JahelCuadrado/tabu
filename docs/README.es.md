<div align="center">

# 🐙 Tabu

**Una elegante barra de pestañas flotante para Windows que te permite cambiar entre ventanas abiertas en un instante.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Disponible en:** [English](../README.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ ¿Qué es Tabu?

Tabu es una **barra de pestañas ligera y siempre visible** que se sitúa en la parte superior de tu pantalla — como las pestañas de un navegador, pero para todas tus ventanas abiertas. Olvídate del ciclo de `Alt+Tab` o de buscar en la barra de tareas. Solo haz clic y cambia.

<div align="center">

| 🎯 **Clic para Cambiar** | 🖥️ **Multi-Monitor** | 🎨 **Totalmente Personalizable** |
|:-:|:-:|:-:|
| Un clic, cambio instantáneo | Funciona en todas las pantallas | Temas, colores y mucho más |

</div>

---

## 🚀 Funcionalidades

### 🪟 Gestión Inteligente de Ventanas
- **Detección en tiempo real** — Descubre y rastrea automáticamente todas las ventanas abiertas
- **Cambio con un clic** — Haz clic en cualquier pestaña para enfocar esa ventana al instante
- **Cerrar ventanas** — Pasa el cursor sobre una pestaña para mostrar el botón de cierre
- **Orden estable** — Las pestañas mantienen posiciones consistentes a medida que se abren y cierran ventanas

### 🎨 Personalización Profunda

<table>
<tr>
<td width="50%">

**🌗 Temas**
- Sistema (detección automática)
- Oscuro
- Claro

**🎨 10 Colores de Acento**

🟣 Morado · 🔵 Azul · 🔵 Cian · 🟢 Verde azulado · 🟢 Verde
🟡 Amarillo · 🟠 Naranja · 🔴 Rojo · 🩷 Rosa · 🌹 Rosa intenso

</td>
<td width="50%">

**⚙️ Ajustes**
- Opacidad de la barra (30%–100%)
- Ancho de pestaña fijo o proporcional
- Mostrar u ocultar la marca
- Modo por monitor o todos los monitores
- Detección en la misma pantalla o en todas las ventanas

</td>
</tr>
</table>

### 🌍 10 Idiomas
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Compatibilidad Multi-Monitor
- Muestra la barra en tu **monitor principal** o en **todos los monitores**
- Opcionalmente, muestra solo las ventanas de la **misma pantalla**

---

## 📦 Primeros Pasos

### Requisitos previos
- **Windows 10/11**
- [SDK de .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior

### Compilar y Ejecutar

```bash
# Clonar el repositorio
git clone https://github.com/your-username/tabu.git
cd tabu

# Compilar
dotnet build Tabu.sln

# Ejecutar
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Arquitectura

Tabu sigue los principios de **Clean Architecture** con una clara separación de responsabilidades:

```
Tabu.sln
├── 📦 Tabu.Domain          Entidades e interfaces del núcleo
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Casos de uso y orquestación
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Integraciones externas
│   ├── Win32/               Detección de ventanas vía P/Invoke
│   └── Persistence/         Almacenamiento de configuración en JSON
│
└── 📦 Tabu.UI               Capa de presentación WPF
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM con CommunityToolkit
    ├── Services/            Gestores de Tema, Acento y Localización
    ├── Locales/             10 archivos de recursos de idioma
    └── Styles/              Recursos de tema Oscuro y Claro
```

### Stack Tecnológico

| Componente | Tecnología |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Patrón** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistencia** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Persistencia de Ajustes

Todos los ajustes se guardan automáticamente en `%LocalAppData%\Tabu\settings.json` cada vez que los modificas. Si el archivo falta o está corrupto, Tabu vuelve elegantemente a valores por defecto sensatos.

---

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Siéntete libre de abrir un issue o enviar un pull request.

1. Haz un fork del repositorio
2. Crea tu rama de funcionalidad (`git checkout -b feature/funcion-increible`)
3. Confirma tus cambios (`git commit -m 'Añade función increíble'`)
4. Sube los cambios a la rama (`git push origin feature/funcion-increible`)
5. Abre un Pull Request

---

## 📄 Licencia

Este proyecto está licenciado bajo la **Licencia Pública General GNU v3.0 (GPL-3.0)**. Eres libre de usar, modificar y distribuir el software bajo los términos de esta licencia. Consulta el archivo [LICENSE](../LICENSE) para más detalles.

---

<div align="center">

**Hecho con ❤️ para usuarios avanzados de Windows**

🐙 *Tabu — Tus ventanas, a un solo clic.*

</div>
