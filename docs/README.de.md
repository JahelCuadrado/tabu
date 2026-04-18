<div align="center">

# 🐙 Tabu

**Eine elegante, schwebende Tab-Leiste für Windows, mit der Sie blitzschnell zwischen offenen Fenstern wechseln können.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Verfügbar in:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Was ist Tabu?

Tabu ist eine **leichtgewichtige, immer im Vordergrund befindliche Tab-Leiste**, die sich am oberen Bildschirmrand befindet — wie Browser-Tabs, aber für all Ihre offenen Fenster. Kein `Alt+Tab`-Wechsel oder Suchen in der Taskleiste mehr. Einfach klicken und wechseln.

<div align="center">

| 🎯 **Zum Wechseln klicken** | 🖥️ **Multi-Monitor** | 🎨 **Voll anpassbar** |
|:-:|:-:|:-:|
| Ein Klick, sofortiger Wechsel | Funktioniert auf allen Bildschirmen | Themes, Farben & mehr |

</div>

---

## 🚀 Funktionen

### 🪟 Intelligente Fensterverwaltung
- **Echtzeit-Erkennung** — Erkennt und verfolgt automatisch alle offenen Fenster
- **Wechsel mit einem Klick** — Klicken Sie auf einen Tab, um das Fenster sofort in den Vordergrund zu bringen
- **Fenster schließen** — Bewegen Sie den Mauszeiger über einen Tab, um die Schaltfläche zum Schließen anzuzeigen
- **Stabile Reihenfolge** — Tabs behalten beim Öffnen und Schließen von Fenstern konsistente Positionen

### 🎨 Tiefgehende Anpassung

<table>
<tr>
<td width="50%">

**🌗 Themes**
- System (automatische Erkennung)
- Dunkel
- Hell

**🎨 10 Akzentfarben**

🟣 Lila · 🔵 Blau · 🔵 Cyan · 🟢 Türkis · 🟢 Grün
🟡 Gelb · 🟠 Orange · 🔴 Rot · 🩷 Pink · 🌹 Rosa

</td>
<td width="50%">

**⚙️ Einstellungen**
- Leistentransparenz (30%–100%)
- Feste oder proportionale Tab-Breite
- Branding ein-/ausblenden
- Pro-Monitor- oder Alle-Monitore-Modus
- Erkennung auf demselben Bildschirm oder allen Fenstern

</td>
</tr>
</table>

### 🌍 10 Sprachen
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Multi-Monitor-fähig
- Zeigen Sie die Leiste auf Ihrem **Primärmonitor** oder auf **allen Monitoren** an
- Optional nur Fenster vom **gleichen Bildschirm** anzeigen

---

## 📦 Erste Schritte

### Voraussetzungen
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) oder höher

### Erstellen & Ausführen

```bash
# Repository klonen
git clone https://github.com/your-username/tabu.git
cd tabu

# Erstellen
dotnet build Tabu.sln

# Ausführen
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Architektur

Tabu folgt den Prinzipien der **Clean Architecture** mit klarer Trennung der Zuständigkeiten:

```
Tabu.sln
├── 📦 Tabu.Domain          Kernentitäten und Schnittstellen
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Anwendungsfälle & Orchestrierung
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Externe Integrationen
│   ├── Win32/               Fenstererkennung via P/Invoke
│   └── Persistence/         JSON-Einstellungsspeicher
│
└── 📦 Tabu.UI               WPF-Präsentationsschicht
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM mit CommunityToolkit
    ├── Services/            Theme-, Akzent-, Lokalisierungsmanager
    ├── Locales/             10 Sprachressourcendateien
    └── Styles/              Dunkel- und Hell-Theme-Ressourcen
```

### Tech-Stack

| Komponente | Technologie |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Muster** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistenz** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Persistenz der Einstellungen

Alle Einstellungen werden bei Änderung automatisch unter `%LocalAppData%\Tabu\settings.json` gespeichert. Bei fehlender oder beschädigter Datei greift Tabu reibungslos auf sinnvolle Standardwerte zurück.

---

## 🤝 Mitwirken

Beiträge sind willkommen! Eröffnen Sie gerne ein Issue oder reichen Sie einen Pull Request ein.

1. Forken Sie das Repository
2. Erstellen Sie Ihren Feature-Branch (`git checkout -b feature/tolles-feature`)
3. Committen Sie Ihre Änderungen (`git commit -m 'Tolles Feature hinzugefügt'`)
4. Pushen Sie den Branch (`git push origin feature/tolles-feature`)
5. Öffnen Sie einen Pull Request

---

## 📄 Lizenz

Dieses Projekt ist unter der **GNU General Public License v3.0 (GPL-3.0)** lizenziert. Sie können die Software gemäß den Bedingungen dieser Lizenz frei verwenden, modifizieren und verbreiten. Siehe die Datei [LICENSE](../LICENSE) für Details.

---

<div align="center">

**Mit ❤️ für Windows-Power-User entwickelt**

🐙 *Tabu — Ihre Fenster, einen Klick entfernt.*

</div>
