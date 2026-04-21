# Tabu — Manual Smoke Test Checklist

> **Run before every release.** Unit tests cover pure logic; this checklist exercises the WPF / Win32 / multi-monitor surface that no harness can reliably automate.
>
> Estimated time: ~10–15 minutes.

## Environment
- [ ] OS: ____________________ (e.g. Windows 11 Pro 26200.8246)
- [ ] Display setup: ____________________ (single / dual / triple, mixed DPI?)
- [ ] Build under test: ____________________ (e.g. v1.4.0 from `version-1.4.0` branch)
- [ ] Tester: ____________________

---

## 1 · Bar lifecycle

- [ ] App launches with no console / error window.
- [ ] Bar appears at the top of the **primary** monitor immediately.
- [ ] Bar height matches the chosen `BarSize` (Small / Medium / Large).
- [ ] Maximizing a window respects the bar (the maximized window does NOT cover it).
- [ ] Closing via the **X** button quits the entire app (no orphan secondary bars).

## 2 · Tab tracking

- [ ] Opening Notepad creates a tab within ≤ 1 s.
- [ ] Renaming a window updates its tab title (try renaming a Notepad file).
- [ ] Closing a window removes its tab.
- [ ] Clicking a tab brings that window to the front.
- [ ] Clicking the **active** tab again does nothing destructive.
- [ ] Middle-click on a tab closes the underlying window.
- [ ] Closing the last window leaves the bar empty (but visible).

## 3 · Drag & drop reordering

- [ ] Drag a tab horizontally → it follows the cursor 1:1.
- [ ] Cross another tab's center → instant swap, no flicker.
- [ ] Release → the dragged tab snaps to its new slot smoothly.
- [ ] Quick click without drag → switches windows (no accidental reorder).

## 4 · Multi-monitor (skip if single display)

- [ ] Enable **"Show on all monitors"** in Settings → secondary bars appear.
- [ ] Each secondary bar shows the same tab list by default.
- [ ] Enable **"Detect same screen only"** → each bar shows only its monitor's apps.
- [ ] Move a window between monitors → tab follows the correct bar within ≤ 1 s.
- [ ] Disable **"Show on all monitors"** → secondary bars disappear cleanly (no ghost AppBar reservations).

## 5 · Fullscreen detection (regression v1.3.1)

- [ ] Open a browser, press **F11** → bar on that monitor hides.
- [ ] Exit F11 → bar returns within ≤ 0.5 s.
- [ ] In multi-monitor, F11 on monitor #1 must NOT hide the bar on monitor #2.
- [ ] **Minimize ALL windows on a monitor** → bar STAYS visible (regression test for v1.3.0 → v1.3.1 fix).
- [ ] Open Task View (Win+Tab) → bar may temporarily hide; closing Task View restores it.

## 6 · Idle resilience (regression v1.3.2)

- [ ] Open 3+ apps and let the screen turn off (Power & Sleep → Screen).
- [ ] Wait at least 2 minutes past sleep transition.
- [ ] Wake the display → **all tabs must still be present** in the same order.
- [ ] Switch virtual desktops (Ctrl+Win+→) and back → tabs persist.

## 6.b · UWP / WinUI quirks (regression v1.4.0)

- [ ] Open **Calculator** → tab appears within ≤ 0.5 s with the calculator icon (NOT a generic stock icon).
- [ ] Close Calculator → tab disappears.
- [ ] Reopen Calculator → tab reappears with the **real package logo** in ≤ 0.3 s (no transient generic icon left behind).
- [ ] Repeat the cycle with **Reloj / Clock** → same behaviour.
- [ ] Open **Telegram**, double-click a photo → "Visor multimedia" tab appears.
- [ ] Click outside the photo to dismiss → tab disappears within ≤ 0.5 s (no ghost tab left behind, regression guard for the v1.4.0 cloak-reason fix).

## 7 · Auto-hide

- [ ] Enable **Auto-hide** in Settings → bar slides up out of view.
- [ ] Move cursor to the very top of the screen → bar slides down.
- [ ] Move cursor away → bar slides up after a small delay.
- [ ] Disable Auto-hide → bar restores its AppBar reservation (maximized windows shrink to fit).

## 8 · Theme & appearance

- [ ] Switch theme System → Light → Dark → System: each transition is instant and clean (no flash).
- [ ] Change accent color → tabs and active highlight refresh immediately.
- [ ] Change opacity slider → bar fades correctly; tabs stay readable at the lowest setting.
- [ ] Change `BarSize` (Small / Medium / Large) → bar resizes, AppBar reservation updates, layout proportions preserved.

## 9 · Acrylic blur (CRITICAL after v1.4.0 refactor)

- [ ] Toggle **Use blur effect** ON: bar background shows the blurred desktop wallpaper.
- [ ] Compare against Windows Calculator: blur intensity should be visually similar.
- [ ] Toggle OFF: bar reverts to the themed solid background (or user opacity).
- [ ] On a Win10 1809+ machine: blur falls back to legacy Acrylic — still visible.
- [ ] On a Win11 22H2+ Enterprise machine: blur uses `DwmSetWindowAttribute` and IS visible (this is the v1.4.0 motivating fix).
- [ ] On a machine where the OS denies acrylic (Transparency = OFF): toggling shows a graceful fallback (solid tinted bar) without crashing.
- [ ] Theme switches while blur is ON: bar stays readable, no rendering artifacts.

## 10 · Settings persistence

- [ ] Change every setting once.
- [ ] Close Tabu via Close button.
- [ ] Relaunch → every setting is restored exactly.
- [ ] Locate `%LOCALAPPDATA%\Tabu\settings.json` → file is human-readable JSON.

## 11 · Updates

- [ ] Press **Check for updates now** with no newer release available → silent / informative toast.
- [ ] Simulate by pointing to a fake release (or trust an actual newer release): updater offers download.
- [ ] Disable **Auto-check for updates** → next startup performs no network I/O (verify with `netstat -bn` or Process Monitor).

## 12 · Startup & system integration

- [ ] Toggle **Launch at startup** ON → registry value `HKCU\…\Run\Tabu` exists and points to the EXE.
- [ ] Toggle OFF → registry value is removed.
- [ ] Reboot → if enabled, Tabu launches automatically with no UAC prompt.

## 13 · Performance & resources

- [ ] Idle CPU under 2 % (Task Manager → Details → `Tabu.UI.exe`).
- [ ] Memory stable over 30 min (no monotonic growth → handle/leak regression).
- [ ] Handle count (Task Manager column) under 1000, stable.
- [ ] Closing the app fully releases the AppBar reservation (try maximizing a window after exit — it must use the full screen).

## 14 · Localization

- [ ] Switch language to **Spanish** → every label translates immediately.
- [ ] Switch to **Japanese** / **Chinese** → CJK characters render correctly (no boxes).
- [ ] Switch back to **English** → no leftover translated strings.

## 15 · Crash safety

- [ ] Close Tabu while dragging a tab → no crash.
- [ ] Disconnect a monitor while bar is on it → app survives, bars rebalance.
- [ ] Reconnect monitor → bar returns when "Show on all monitors" is enabled.

---

## Sign-off

- [ ] All checks above passed
- [ ] Issues found (link to GitHub issues): ____________________
- [ ] Approved for release: ____________________ (signature / handle, date)
