<div align="center">

# 🐙 Tabu

**開いているウィンドウを一瞬で切り替えられる、Windows 用の洗練されたフローティングタブバー。**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **対応言語:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Tabu とは？

Tabu は画面上部に配置される、**軽量で常に最前面に表示されるタブバー**です — ブラウザのタブのように、開いているすべてのウィンドウに対応します。`Alt+Tab` での切り替えやタスクバーから探す必要はもうありません。クリックするだけで切り替えられます。

<div align="center">

| 🎯 **クリックで切替** | 🖥️ **マルチモニター** | 🎨 **完全カスタマイズ可能** |
|:-:|:-:|:-:|
| ワンクリックで瞬時に切替 | すべてのモニターで動作 | テーマ、色、その他 |

</div>

---

## 🚀 機能

### 🪟 スマートなウィンドウ管理
- **リアルタイム検出** — 開いているすべてのウィンドウを自動的に検出して追跡
- **ワンクリック切替** — タブをクリックするだけで該当ウィンドウを瞬時にフォーカス
- **ウィンドウを閉じる** — タブにマウスを乗せると閉じるボタンが表示されます
- **安定した順序** — ウィンドウの開閉に関わらずタブの位置は一貫して保たれます

### 🎨 詳細なカスタマイズ

<table>
<tr>
<td width="50%">

**🌗 テーマ**
- システム (自動検出)
- ダーク
- ライト

**🎨 10 種類のアクセントカラー**

🟣 紫 · 🔵 青 · 🔵 シアン · 🟢 ティール · 🟢 緑
🟡 黄 · 🟠 オレンジ · 🔴 赤 · 🩷 ピンク · 🌹 ローズ

</td>
<td width="50%">

**⚙️ 設定**
- バーの不透明度 (30%–100%)
- 固定幅または比例幅のタブ
- ブランディングの表示/非表示
- モニターごと または 全モニターモード
- 同じ画面または全ウィンドウの検出

</td>
</tr>
</table>

### 🌍 10 言語対応
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ マルチモニター対応
- バーを **メインモニター** または **すべてのモニター** に表示
- 任意で **同じ画面** のウィンドウのみを表示

---

## 📦 はじめに

### 必要要件
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 以上

### ビルドと実行

```bash
# リポジトリをクローン
git clone https://github.com/your-username/tabu.git
cd tabu

# ビルド
dotnet build Tabu.sln

# 実行
dotnet run --project src/Tabu.UI
```

---

## 🏗️ アーキテクチャ

Tabu は責務の明確な分離を伴う **Clean Architecture** の原則に従います:

```
Tabu.sln
├── 📦 Tabu.Domain          コアエンティティとインターフェース
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      ユースケースとオーケストレーション
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   外部統合
│   ├── Win32/               P/Invoke によるウィンドウ検出
│   └── Persistence/         JSON 設定ストレージ
│
└── 📦 Tabu.UI               WPF プレゼンテーション層
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          CommunityToolkit による MVVM
    ├── Services/            テーマ・アクセント・ローカライズ管理
    ├── Locales/             10 言語のリソースファイル
    └── Styles/              ダーク・ライトテーマのリソース
```

### 技術スタック

| コンポーネント | 技術 |
|-----------|-----------|
| **フレームワーク** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **パターン** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **永続化** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ 設定の永続化

設定を変更するたびに、自動的に `%LocalAppData%\Tabu\settings.json` に保存されます。ファイルが見つからないか破損している場合、Tabu は適切なデフォルト値に滑らかにフォールバックします。

---

## 🤝 コントリビューション

コントリビューションを歓迎します！お気軽に Issue を開くか、Pull Request を送信してください。

1. リポジトリをフォーク
2. フィーチャーブランチを作成 (`git checkout -b feature/amazing-feature`)
3. 変更をコミット (`git commit -m 'Add amazing feature'`)
4. ブランチにプッシュ (`git push origin feature/amazing-feature`)
5. Pull Request を開く

---

## 📄 ライセンス

このプロジェクトは **GNU General Public License v3.0 (GPL-3.0)** の下でライセンスされています。本ライセンスの条項に従って自由に使用、変更、配布できます。詳細は [LICENSE](../LICENSE) ファイルをご覧ください。

---

<div align="center">

**Windows パワーユーザーのために ❤️ を込めて**

🐙 *Tabu — あなたのウィンドウを、ワンクリックで。*

</div>
