<div align="center">

# 🐙 Tabu

**A sleek, floating tab bar for Windows that lets you switch between open windows in a snap.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Available in:** [Español](docs/README.es.md) · [Français](docs/README.fr.md) · [Deutsch](docs/README.de.md) · [Português](docs/README.pt.md) · [Italiano](docs/README.it.md) · [日本語](docs/README.ja.md) · [中文](docs/README.zh.md) · [한국어](docs/README.ko.md) · [Русский](docs/README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ What is Tabu?

Tabu is a **lightweight, always-on-top tab bar** that sits at the top of your screen — like browser tabs, but for all your open windows. No more `Alt+Tab` cycling or hunting through the taskbar. Just click and switch.

<div align="center">

| 🎯 **Click to Switch** | 🖥️ **Multi-Monitor** | 🎨 **Fully Customizable** |
|:-:|:-:|:-:|
| One click, instant switch | Works across all screens | Themes, colors & more |

</div>

---

## 🚀 Features

### 🪟 Smart Window Management
- **Real-time detection** — Automatically discovers and tracks all open windows
- **One-click switching** — Click any tab to instantly bring that window to focus
- **Close windows** — Hover over a tab to reveal the close button
- **Stable ordering** — Tabs maintain consistent positions as windows open and close

### 🎨 Deep Customization

<table>
<tr>
<td width="50%">

**🌗 Themes**
- System (auto-detect)
- Dark
- Light

**🎨 10 Accent Colors**

🟣 Purple · 🔵 Blue · 🔵 Cyan · 🟢 Teal · 🟢 Green
🟡 Yellow · 🟠 Orange · 🔴 Red · 🩷 Pink · 🌹 Rose

</td>
<td width="50%">

**⚙️ Settings**
- Bar opacity (30%–100%)
- Fixed or proportional tab width
- Show/hide branding
- Per-monitor or all-monitors mode
- Same-screen or all-windows detection

</td>
</tr>
</table>

### 🌍 10 Languages
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Multi-Monitor Ready
- Display the bar on your **primary monitor** or on **all monitors**
- Optionally show only windows from the **same screen**

---

## 📦 Getting Started

### Prerequisites
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build & Run

```bash
# Clone the repository
git clone https://github.com/your-username/tabu.git
cd tabu

# Build
dotnet build Tabu.sln

# Run
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Architecture

Tabu follows **Clean Architecture** principles with clear separation of concerns:

```
Tabu.sln
├── 📦 Tabu.Domain          Core entities & interfaces
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Use cases & orchestration
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   External integrations
│   ├── Win32/               Window detection via P/Invoke
│   └── Persistence/         JSON settings storage
│
└── 📦 Tabu.UI               WPF presentation layer
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM with CommunityToolkit
    ├── Services/            Theme, Accent, Localization managers
    ├── Locales/             10 language resource files
    └── Styles/              Dark & Light theme resources
```

### Tech Stack

| Component | Technology |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Pattern** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistence** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Settings Persistence

All settings are automatically saved to `%LocalAppData%\Tabu\settings.json` whenever you change them. If the file is missing or corrupted, Tabu gracefully falls back to sensible defaults.

---

## 🤝 Contributing

Contributions are welcome! Feel free to open an issue or submit a pull request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**. You are free to use, modify, and distribute the software under the terms of this license. See the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Made with ❤️ for Windows power users**

🐙 *Tabu — Your windows, one click away.*

</div>
