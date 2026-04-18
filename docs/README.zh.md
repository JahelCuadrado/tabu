<div align="center">

# 🐙 Tabu

**一款简洁的 Windows 浮动标签栏，让你瞬间在打开的窗口之间切换。**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **支持语言：** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ 什么是 Tabu？

Tabu 是一款**轻量级、始终置顶的标签栏**，停留在屏幕顶部 — 就像浏览器标签，但适用于你所有打开的窗口。无需再 `Alt+Tab` 循环切换或在任务栏中翻找。点击即可切换。

<div align="center">

| 🎯 **点击切换** | 🖥️ **多显示器** | 🎨 **完全可定制** |
|:-:|:-:|:-:|
| 一键即时切换 | 适用于所有屏幕 | 主题、颜色等 |

</div>

---

## 🚀 功能

### 🪟 智能窗口管理
- **实时检测** — 自动发现并跟踪所有打开的窗口
- **一键切换** — 点击任意标签即可立即将该窗口前置
- **关闭窗口** — 将鼠标悬停在标签上以显示关闭按钮
- **稳定排序** — 窗口打开和关闭时，标签位置始终保持一致

### 🎨 深度定制

<table>
<tr>
<td width="50%">

**🌗 主题**
- 跟随系统（自动检测）
- 深色
- 浅色

**🎨 10 种强调色**

🟣 紫色 · 🔵 蓝色 · 🔵 青色 · 🟢 蓝绿色 · 🟢 绿色
🟡 黄色 · 🟠 橙色 · 🔴 红色 · 🩷 粉色 · 🌹 玫红

</td>
<td width="50%">

**⚙️ 设置**
- 标签栏不透明度 (30%–100%)
- 固定或比例标签宽度
- 显示/隐藏品牌标识
- 单显示器或全显示器模式
- 同屏或全部窗口检测

</td>
</tr>
</table>

### 🌍 支持 10 种语言
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ 多显示器支持
- 在**主显示器**或**所有显示器**上显示标签栏
- 可选只显示**同一屏幕**上的窗口

---

## 📦 快速开始

### 先决条件
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本

### 构建与运行

```bash
# 克隆仓库
git clone https://github.com/your-username/tabu.git
cd tabu

# 构建
dotnet build Tabu.sln

# 运行
dotnet run --project src/Tabu.UI
```

---

## 🏗️ 架构

Tabu 遵循 **Clean Architecture** 原则，关注点清晰分离：

```
Tabu.sln
├── 📦 Tabu.Domain          核心实体与接口
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      用例与编排
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   外部集成
│   ├── Win32/               通过 P/Invoke 进行窗口检测
│   └── Persistence/         JSON 配置存储
│
└── 📦 Tabu.UI               WPF 表现层
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          基于 CommunityToolkit 的 MVVM
    ├── Services/            主题、强调色、本地化管理器
    ├── Locales/             10 个语言资源文件
    └── Styles/              深色和浅色主题资源
```

### 技术栈

| 组件 | 技术 |
|-----------|-----------|
| **框架** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **模式** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **持久化** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ 设置持久化

每当你修改设置，所有设置都会自动保存到 `%LocalAppData%\Tabu\settings.json`。如果文件丢失或损坏，Tabu 会优雅地回退到合理的默认值。

---

## 🤝 贡献

欢迎贡献！请随时提交 issue 或 pull request。

1. Fork 本仓库
2. 创建你的功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交你的更改 (`git commit -m 'Add amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 提交 Pull Request

---

## 📄 许可证

本项目采用 **GNU 通用公共许可证 v3.0 (GPL-3.0)** 授权。你可以根据本许可证的条款自由使用、修改和分发本软件。详见 [LICENSE](../LICENSE) 文件。

---

<div align="center">

**用 ❤️ 为 Windows 高级用户打造**

🐙 *Tabu — 你的窗口，一键直达。*

</div>
