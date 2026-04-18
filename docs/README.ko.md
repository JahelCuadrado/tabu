<div align="center">

# 🐙 Tabu

**열려 있는 창 사이를 순식간에 전환할 수 있는, Windows용 세련된 플로팅 탭 바.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPL%20v3-blue?style=for-the-badge)](../LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)

<br/>

🌍 **지원 언어:** [English](../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [中文](README.zh.md) · [Русский](README.ru.md)

<br/>

<img src="https://img.shields.io/badge/-%F0%9F%90%99%20Tabu-1a1a2e?style=for-the-badge&labelColor=1a1a2e" alt="Tabu" height="40"/>

</div>

---

## ✨ Tabu란?

Tabu는 화면 상단에 자리 잡는 **가볍고 항상 위에 표시되는 탭 바**입니다 — 브라우저 탭처럼, 모든 열려 있는 창에 적용됩니다. `Alt+Tab` 으로 순환하거나 작업 표시줄을 뒤질 필요가 없습니다. 클릭만 하면 전환됩니다.

<div align="center">

| 🎯 **클릭으로 전환** | 🖥️ **다중 모니터** | 🎨 **완전 사용자 정의** |
|:-:|:-:|:-:|
| 한 번의 클릭, 즉시 전환 | 모든 화면에서 작동 | 테마, 색상 등 |

</div>

---

## 🚀 기능

### 🪟 스마트 창 관리
- **실시간 감지** — 열려 있는 모든 창을 자동으로 검색하고 추적합니다
- **원클릭 전환** — 탭을 클릭하면 즉시 해당 창에 포커스됩니다
- **창 닫기** — 탭 위에 마우스를 올리면 닫기 버튼이 나타납니다
- **안정적인 순서** — 창이 열리고 닫혀도 탭의 위치는 일관되게 유지됩니다

### 🎨 깊이 있는 커스터마이징

<table>
<tr>
<td width="50%">

**🌗 테마**
- 시스템 (자동 감지)
- 다크
- 라이트

**🎨 10가지 강조 색상**

🟣 보라 · 🔵 파랑 · 🔵 청록 · 🟢 틸 · 🟢 녹색
🟡 노랑 · 🟠 주황 · 🔴 빨강 · 🩷 핑크 · 🌹 로즈

</td>
<td width="50%">

**⚙️ 설정**
- 바 불투명도 (30%–100%)
- 고정 또는 비례 탭 너비
- 브랜딩 표시/숨김
- 모니터별 또는 모든 모니터 모드
- 같은 화면 또는 모든 창 감지

</td>
</tr>
</table>

### 🌍 10개 언어 지원
English · Español · Français · Deutsch · Português · Italiano · 日本語 · 中文 · 한국어 · Русский

### 🖥️ 다중 모니터 지원
- **주 모니터** 또는 **모든 모니터**에 바를 표시합니다
- 선택적으로 **같은 화면**의 창만 표시합니다

---

## 📦 시작하기

### 사전 요구 사항
- **Windows 10/11**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 이상

### 빌드 및 실행

```bash
# 저장소 복제
git clone https://github.com/your-username/tabu.git
cd tabu

# 빌드
dotnet build Tabu.sln

# 실행
dotnet run --project src/Tabu.UI
```

---

## 🏗️ 아키텍처

Tabu는 명확한 관심사 분리를 갖춘 **Clean Architecture** 원칙을 따릅니다:

```
Tabu.sln
├── 📦 Tabu.Domain          핵심 엔터티 및 인터페이스
│   ├── Entities/            TrackedWindow, ScreenInfo, UserSettings
│   └── Interfaces/          IWindowDetector, ISettingsRepository
│
├── 📦 Tabu.Application      유스케이스 및 오케스트레이션
│   └── Services/            WindowSwitcher
│
├── 📦 Tabu.Infrastructure   외부 통합
│   ├── Win32/               P/Invoke를 통한 창 감지
│   └── Persistence/         JSON 설정 저장소
│
└── 📦 Tabu.UI               WPF 프레젠테이션 계층
    ├── Views/               MainWindow, SettingsWindow
    ├── ViewModels/          CommunityToolkit 기반 MVVM
    ├── Services/            테마, 강조색, 지역화 관리자
    ├── Locales/             10개 언어 리소스 파일
    └── Styles/              다크 및 라이트 테마 리소스
```

### 기술 스택

| 구성 요소 | 기술 |
|-----------|-----------|
| **프레임워크** | .NET 8.0 |
| **UI** | WPF (Windows Presentation Foundation) |
| **패턴** | MVVM + Clean Architecture |
| **MVVM** | CommunityToolkit.Mvvm |
| **DI** | Microsoft.Extensions.Hosting |
| **Win32** | P/Invoke (EnumWindows, SetForegroundWindow) |
| **영속성** | JSON (`%LocalAppData%\Tabu\settings.json`) |

---

## ⚙️ 설정 영속성

모든 설정은 변경할 때마다 `%LocalAppData%\Tabu\settings.json`에 자동으로 저장됩니다. 파일이 없거나 손상된 경우 Tabu는 합리적인 기본값으로 부드럽게 폴백합니다.

---

## 🤝 기여하기

기여를 환영합니다! 자유롭게 이슈를 열거나 pull request를 보내주세요.

1. 저장소 포크
2. 기능 브랜치 생성 (`git checkout -b feature/amazing-feature`)
3. 변경 사항 커밋 (`git commit -m 'Add amazing feature'`)
4. 브랜치에 푸시 (`git push origin feature/amazing-feature`)
5. Pull Request 열기

---

## 📄 라이선스

이 프로젝트는 **GNU General Public License v3.0 (GPL-3.0)** 으로 라이선스됩니다. 본 라이선스의 조건에 따라 자유롭게 사용, 수정 및 배포할 수 있습니다. 자세한 내용은 [LICENSE](../LICENSE) 파일을 참조하세요.

---

<div align="center">

**Windows 파워 유저를 위해 ❤️ 로 제작되었습니다**

🐙 *Tabu — 당신의 창, 한 번의 클릭으로.*

</div>
