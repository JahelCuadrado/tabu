<div align="center">

# 🐙 Tabu

**Элегантная плавающая панель вкладок для Windows, позволяющая мгновенно переключаться между открытыми окнами.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Доступно на:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Что такое Tabu?

Tabu — это **лёгкая, всегда поверх других окон панель вкладок**, расположенная в верхней части экрана — как вкладки браузера, но для всех ваших открытых окон. Больше никакого циклического `Alt+Tab` или поиска в панели задач. Просто кликайте и переключайтесь.

<div align="center">

| 🎯 **Клик для переключения** | 🖥️ **Несколько мониторов** | 🎨 **Полностью настраиваемая** |
|:-:|:-:|:-:|
| Один клик — мгновенный переход | Работает на всех экранах | Темы, цвета и многое другое |

</div>

---

## 🚀 Возможности

### 🪟 Умное управление окнами
- **Обнаружение в реальном времени** — Автоматически находит и отслеживает все открытые окна
- **Переключение в один клик** — Кликните по любой вкладке, чтобы мгновенно сфокусировать соответствующее окно
- **Закрытие окон** — Наведите курсор на вкладку, чтобы появилась кнопка закрытия
- **Стабильный порядок** — Вкладки сохраняют последовательное расположение при открытии и закрытии окон

### 🎨 Глубокая настройка

<table>
<tr>
<td width="50%">

**🌗 Темы**
- Системная (автоопределение)
- Тёмная
- Светлая

**🎨 10 акцентных цветов**

🟣 Фиолетовый · 🔵 Синий · 🔵 Голубой · 🟢 Бирюзовый · 🟢 Зелёный
🟡 Жёлтый · 🟠 Оранжевый · 🔴 Красный · 🩷 Розовый · 🌹 Розово-красный

</td>
<td width="50%">

**⚙️ Настройки**
- Прозрачность панели (30%–100%)
- Фиксированная или пропорциональная ширина вкладки
- Показ/скрытие брендинга
- Режим «по монитору» или «все мониторы»
- Обнаружение на том же экране или всех окон

</td>
</tr>
</table>

### 🌍 10 языков
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Поддержка нескольких мониторов
- Отображение панели на **основном мониторе** или на **всех мониторах**
- При желании показывать только окна с **того же экрана**

---

## 📦 Начало работы

### Требования
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) или новее

### Сборка и запуск

```bash
# Клонировать репозиторий
git clone https://github.com/your-username/tabu.git
cd tabu

# Сборка
dotnet build Tabu.sln

# Запуск
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Архитектура

Tabu следует принципам **Clean Architecture** с чётким разделением обязанностей:

```
Tabu.sln
├── 📦 Tabu.Domain          Основные сущности и интерфейсы
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Сценарии использования и оркестрация
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Внешние интеграции
│   ├── Win32/               Обнаружение окон через P/Invoke
│   └── Persistence/         Хранение настроек в JSON
│
└── 📦 Tabu.UI               Уровень представления WPF
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM с CommunityToolkit
    ├── Services/            Менеджеры темы, акцента, локализации
    ├── Locales/             10 файлов ресурсов локализации
    └── Styles/              Ресурсы тёмной и светлой тем
```

### Технологический стек

| Компонент | Технология |
|-----------|-----------|
| **Фреймворк** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Паттерн** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Хранение** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Сохранение настроек

Все настройки автоматически сохраняются в `%LocalAppData%\Tabu\settings.json` при каждом изменении. Если файл отсутствует или повреждён, Tabu плавно возвращается к разумным значениям по умолчанию.

---

## 🤝 Участие в разработке

Вклад приветствуется! Не стесняйтесь открывать issue или присылать pull request.

1. Сделайте форк репозитория
2. Создайте ветку функциональности (`git checkout -b feature/amazing-feature`)
3. Зафиксируйте изменения (`git commit -m 'Add amazing feature'`)
4. Отправьте ветку (`git push origin feature/amazing-feature`)
5. Откройте Pull Request

---

## 📄 Лицензия

Этот проект распространяется под лицензией **GNU General Public License v3.0 (GPL-3.0)**. Вы можете свободно использовать, изменять и распространять программное обеспечение в соответствии с условиями данной лицензии. Подробности см. в файле [LICENSE](../LICENSE).

---

<div align="center">

**Сделано с ❤️ для опытных пользователей Windows**

🐙 *Tabu — Ваши окна в одном клике.*

</div>
