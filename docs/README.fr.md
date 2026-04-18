<div align="center">

# 🐙 Tabu

**Une élégante barre d'onglets flottante pour Windows qui vous permet de basculer entre vos fenêtres ouvertes en un clin d'œil.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Disponible en :** [English](../README.md) · [Español](README.es.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Qu'est-ce que Tabu ?

Tabu est une **barre d'onglets légère et toujours au premier plan** qui se place en haut de votre écran — comme les onglets de navigateur, mais pour toutes vos fenêtres ouvertes. Fini le `Alt+Tab` à répétition ou la recherche dans la barre des tâches. Cliquez et changez.

<div align="center">

| 🎯 **Cliquer pour Changer** | 🖥️ **Multi-Écran** | 🎨 **Entièrement Personnalisable** |
|:-:|:-:|:-:|
| Un clic, changement instantané | Fonctionne sur tous les écrans | Thèmes, couleurs et plus |

</div>

---

## 🚀 Fonctionnalités

### 🪟 Gestion Intelligente des Fenêtres
- **Détection en temps réel** — Découvre et suit automatiquement toutes les fenêtres ouvertes
- **Changement en un clic** — Cliquez sur n'importe quel onglet pour mettre instantanément cette fenêtre au premier plan
- **Fermer les fenêtres** — Survolez un onglet pour révéler le bouton de fermeture
- **Ordre stable** — Les onglets conservent des positions cohérentes lorsque les fenêtres s'ouvrent et se ferment

### 🎨 Personnalisation Approfondie

<table>
<tr>
<td width="50%">

**🌗 Thèmes**
- Système (détection automatique)
- Sombre
- Clair

**🎨 10 Couleurs d'Accent**

🟣 Violet · 🔵 Bleu · 🔵 Cyan · 🟢 Sarcelle · 🟢 Vert
🟡 Jaune · 🟠 Orange · 🔴 Rouge · 🩷 Rose · 🌹 Rose vif

</td>
<td width="50%">

**⚙️ Paramètres**
- Opacité de la barre (30%–100%)
- Largeur d'onglet fixe ou proportionnelle
- Afficher/masquer la marque
- Mode par écran ou tous les écrans
- Détection sur le même écran ou toutes les fenêtres

</td>
</tr>
</table>

### 🌍 10 Langues
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Prêt pour le Multi-Écran
- Affichez la barre sur votre **écran principal** ou sur **tous les écrans**
- Affichez éventuellement uniquement les fenêtres du **même écran**

---

## 📦 Premiers Pas

### Prérequis
- **Windows 10/11**
- [SDK .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) ou supérieur

### Compiler et Exécuter

```bash
# Cloner le dépôt
git clone https://github.com/your-username/tabu.git
cd tabu

# Compiler
dotnet build Tabu.sln

# Exécuter
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Architecture

Tabu suit les principes de **Clean Architecture** avec une séparation claire des responsabilités :

```
Tabu.sln
├── 📦 Tabu.Domain          Entités et interfaces du noyau
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Cas d'utilisation et orchestration
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Intégrations externes
│   ├── Win32/               Détection des fenêtres via P/Invoke
│   └── Persistence/         Stockage des paramètres en JSON
│
└── 📦 Tabu.UI               Couche de présentation WPF
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM avec CommunityToolkit
    ├── Services/            Gestionnaires de thème, accent, localisation
    ├── Locales/             10 fichiers de ressources de langue
    └── Styles/              Ressources de thème sombre et clair
```

### Stack Technique

| Composant | Technologie |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Modèle** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistance** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Persistance des Paramètres

Tous les paramètres sont automatiquement enregistrés dans `%LocalAppData%\Tabu\settings.json` à chaque modification. Si le fichier est manquant ou corrompu, Tabu revient en douceur à des valeurs par défaut raisonnables.

---

## 🤝 Contribuer

Les contributions sont les bienvenues ! N'hésitez pas à ouvrir une issue ou à soumettre une pull request.

1. Forkez le dépôt
2. Créez votre branche de fonctionnalité (`git checkout -b feature/super-fonction`)
3. Commitez vos modifications (`git commit -m 'Ajout d\'une super fonction'`)
4. Poussez la branche (`git push origin feature/super-fonction`)
5. Ouvrez une Pull Request

---

## 📄 Licence

Ce projet est sous **Licence publique générale GNU v3.0 (GPL-3.0)**. Vous êtes libre d'utiliser, modifier et redistribuer le logiciel selon les termes de cette licence. Consultez le fichier [LICENSE](../LICENSE) pour plus de détails.

---

<div align="center">

**Conçu avec ❤️ pour les utilisateurs avancés de Windows**

🐙 *Tabu — Vos fenêtres, à un clic.*

</div>
