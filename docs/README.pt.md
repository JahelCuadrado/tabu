<div align="center">

# 🐙 Tabu

**Uma elegante barra de abas flutuante para Windows que permite alternar entre janelas abertas num instante.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **Disponível em:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [한국어](README.ko.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ O que é o Tabu?

Tabu é uma **barra de abas leve e sempre no topo** que fica na parte superior do seu ecrã — como as abas do navegador, mas para todas as suas janelas abertas. Sem mais `Alt+Tab` ou caça à barra de tarefas. Basta clicar e alternar.

<div align="center">

| 🎯 **Clicar para Alternar** | 🖥️ **Multi-Monitor** | 🎨 **Totalmente Personalizável** |
|:-:|:-:|:-:|
| Um clique, troca instantânea | Funciona em todos os ecrãs | Temas, cores e muito mais |

</div>

---

## 🚀 Funcionalidades

### 🪟 Gestão Inteligente de Janelas
- **Deteção em tempo real** — Descobre e acompanha automaticamente todas as janelas abertas
- **Troca com um clique** — Clique em qualquer aba para focar instantaneamente essa janela
- **Fechar janelas** — Passe o cursor sobre uma aba para revelar o botão de fechar
- **Ordem estável** — As abas mantêm posições consistentes à medida que as janelas abrem e fecham

### 🎨 Personalização Profunda

<table>
<tr>
<td width="50%">

**🌗 Temas**
- Sistema (deteção automática)
- Escuro
- Claro

**🎨 10 Cores de Destaque**

🟣 Roxo · 🔵 Azul · 🔵 Ciano · 🟢 Verde-azulado · 🟢 Verde
🟡 Amarelo · 🟠 Laranja · 🔴 Vermelho · 🩷 Cor-de-rosa · 🌹 Rosa

</td>
<td width="50%">

**⚙️ Definições**
- Opacidade da barra (30%–100%)
- Largura da aba fixa ou proporcional
- Mostrar/ocultar a marca
- Modo por monitor ou todos os monitores
- Deteção no mesmo ecrã ou em todas as janelas

</td>
</tr>
</table>

### 🌍 10 Idiomas
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ Pronto para Multi-Monitor
- Apresente a barra no seu **monitor principal** ou em **todos os monitores**
- Opcionalmente, mostre apenas as janelas do **mesmo ecrã**

---

## 📦 Começar

### Pré-requisitos
- **Windows 10/11**
- [SDK do .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) ou superior

### Compilar e Executar

```bash
# Clonar o repositório
git clone https://github.com/your-username/tabu.git
cd tabu

# Compilar
dotnet build Tabu.sln

# Executar
dotnet run --project src/Tabu.UI
```

---

## 🏗️ Arquitetura

O Tabu segue os princípios de **Clean Architecture** com uma clara separação de responsabilidades:

```
Tabu.sln
├── 📦 Tabu.Domain          Entidades e interfaces do núcleo
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      Casos de uso e orquestração
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   Integrações externas
│   ├── Win32/               Deteção de janelas via P/Invoke
│   └── Persistence/         Armazenamento de definições em JSON
│
└── 📦 Tabu.UI               Camada de apresentação WPF
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          MVVM com CommunityToolkit
    ├── Services/            Gestores de Tema, Destaque e Localização
    ├── Locales/             10 ficheiros de recursos de idioma
    └── Styles/              Recursos de tema escuro e claro
```

### Stack Tecnológico

| Componente | Tecnologia |
|-----------|-----------|
| **Framework** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **Padrão** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **Persistência** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ Persistência das Definições

Todas as definições são guardadas automaticamente em `%LocalAppData%\Tabu\settings.json` sempre que as altera. Se o ficheiro estiver em falta ou corrompido, o Tabu volta de forma elegante a valores predefinidos sensatos.

---

## 🤝 Contribuir

As contribuições são bem-vindas! Sinta-se à vontade para abrir uma issue ou submeter um pull request.

1. Faça fork do repositório
2. Crie a sua branch de funcionalidade (`git checkout -b feature/funcionalidade-incrivel`)
3. Faça commit das suas alterações (`git commit -m 'Adiciona funcionalidade incrível'`)
4. Faça push para a branch (`git push origin feature/funcionalidade-incrivel`)
5. Abra um Pull Request

---

## 📄 Licença

Este projeto está licenciado sob a **Licença Pública Geral GNU v3.0 (GPL-3.0)**. Tem liberdade para usar, modificar e distribuir o software ao abrigo dos termos desta licença. Consulte o ficheiro [LICENSE](../LICENSE) para mais detalhes.

---

<div align="center">

**Feito com ❤️ para utilizadores avançados de Windows**

🐙 *Tabu — As suas janelas, a um clique de distância.*

</div>
