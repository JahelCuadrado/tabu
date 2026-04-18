<div align="center">

# 🐙 Tabu

**Un'elegante barra di schede fluttuante per Windows che ti permette di passare tra le finestre aperte in un attimo.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Disponibile in:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Cos'è Tabu?

Tabu è una **barra di schede leggera e sempre in primo piano** che si posiziona nella parte superiore dello schermo — come le schede del browser, ma per tutte le tue finestre aperte. Niente più `Alt+Tab` o ricerche nella barra delle applicazioni. Basta cliccare e cambiare.

<div align="center">

| 🎯 **Clicca per Cambiare** | 🖥️ **Multi-Monitor** | 🎨 **Totalmente Personalizzabile** |
|:-:|:-:|:-:|
| Un clic, cambio istantaneo | Funziona su tutti gli schermi | Temi, colori e altro |

</div>

---

## 🚀 Funzionalità

### 🪟 Gestione Intelligente delle Finestre
- **Rilevamento in tempo reale** — Scopre e tiene traccia automaticamente di tutte le finestre aperte
- **Cambio con un clic** — Clicca su qualsiasi scheda per portare istantaneamente quella finestra in primo piano
- **Chiudi le finestre** — Passa il cursore su una scheda per rivelare il pulsante di chiusura
- **Ordine stabile** — Le schede mantengono posizioni coerenti man mano che le finestre si aprono e si chiudono

### 🎨 Personalizzazione Profonda

<table>
<tr>
<td width="50%">

**🌗 Temi**
- Sistema (rilevamento automatico)
- Scuro
- Chiaro

**🎨 10 Colori d'Accento**

🟣 Viola · 🔵 Blu · 🔵 Ciano · 🟢 Verde acqua · 🟢 Verde
🟡 Giallo · 🟠 Arancione · 🔴 Rosso · 🩷 Rosa · 🌹 Rosa intenso

</td>
<td width="50%">

**⚙️ Impostazioni**
- Opacità della barra (30%–100%)
- Larghezza della scheda fissa o proporzionale
- Mostra/nascondi il branding
- Modalità per monitor o tutti i monitor
- Rilevamento sullo stesso schermo o su tutte le finestre

</td>
</tr>
</table>

### 🌍 10 Lingue
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Pronto per Multi-Monitor
- Visualizza la barra sul tuo **monitor principale** o su **tutti i monitor**
- Opzionalmente, mostra solo le finestre dello **stesso schermo**

---

## 📦 Per Iniziare

### Prerequisiti
- **Windows 10/11**
- [SDK .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superiore

### Compila ed Esegui

```bash
# Clona il repository
git clone https://github.com/your-username/tabu.git
cd tabu

# Compila
dotnet build Tabu.sln

# Esegui
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Architettura

Tabu segue i principi di **Clean Architecture** con una chiara separazione delle responsabilità:

```
Tabu.sln
├── 📦 Tabu.Domain          Entità e interfacce di base
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Casi d'uso e orchestrazione
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Integrazioni esterne
│   ├── Win32/               Rilevamento finestre tramite P/Invoke
│   └── Persistence/         Memorizzazione impostazioni in JSON
│
└── 📦 Tabu.UI               Livello di presentazione WPF
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM con CommunityToolkit
    ├── Services/            Gestori di Tema, Accento, Localizzazione
    ├── Locales/             10 file di risorse linguistiche
    └── Styles/              Risorse tema scuro e chiaro
```

### Stack Tecnologico

| Componente | Tecnologia |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Pattern** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistenza** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Persistenza delle Impostazioni

Tutte le impostazioni vengono salvate automaticamente in `%LocalAppData%\Tabu\settings.json` ogni volta che le modifichi. Se il file è mancante o corrotto, Tabu ricade elegantemente su valori predefiniti sensati.

---

## 🤝 Contribuire

I contributi sono benvenuti! Sentiti libero di aprire un issue o inviare una pull request.

1. Forka il repository
2. Crea il tuo branch di funzionalità (`git checkout -b feature/funzione-fantastica`)
3. Esegui il commit delle tue modifiche (`git commit -m 'Aggiungi funzione fantastica'`)
4. Esegui il push del branch (`git push origin feature/funzione-fantastica`)
5. Apri una Pull Request

---

## 📄 Licenza

Questo progetto è concesso in licenza secondo i termini della **GNU General Public License v3.0 (GPL-3.0)**. Sei libero di usare, modificare e distribuire il software secondo i termini di questa licenza. Consulta il file [LICENSE](../LICENSE) per i dettagli.

---

<div align="center">

**Realizzato con ❤️ per gli utenti esperti di Windows**

🐙 *Tabu — Le tue finestre, a un clic di distanza.*

</div>
