using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Tabu.Application.Services;
using Tabu.Domain.Entities;
using Tabu.UI.Helpers;
using Tabu.UI.Services;

namespace Tabu.UI.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly WindowSwitcher _switcher;
    private readonly DispatcherTimer? _pollTimer;
    private readonly DispatcherTimer _clockTimer;
    private bool _isBarOnAllMonitors;
    private bool _isDetectSameScreenOnly;
    private AppTheme _appTheme = AppTheme.System;
    private double _barOpacity = 1.0;
    private bool _useFixedTabWidth = false;
    private bool _showBranding = true;
    private string _language = "en";
    private string _accentColor = "blue";
    private bool _autoHideBar;
    private bool _launchAtStartup;
    private bool _showClock = true;
    private string _currentTime = string.Empty;
    private BarSize _barSize = BarSize.Small;
    private IntPtr? _monitorFilter;
    private bool _useBlurEffect;
    private bool _autoCheckUpdates = true;

    public ObservableCollection<TabViewModel> Tabs { get; } = new();

    public bool HasTabs => Tabs.Count > 0;

    public bool IsBarOnAllMonitors
    {
        get => _isBarOnAllMonitors;
        set
        {
            if (SetProperty(ref _isBarOnAllMonitors, value))
            {
                BarPlacementChangeRequested?.Invoke(value);
            }
        }
    }

    public bool IsDetectSameScreenOnly
    {
        get => _isDetectSameScreenOnly;
        set
        {
            if (SetProperty(ref _isDetectSameScreenOnly, value))
            {
                DetectionModeChangeRequested?.Invoke(value);
            }
        }
    }

    public IntPtr? MonitorFilter
    {
        get => _monitorFilter;
        set => SetProperty(ref _monitorFilter, value);
    }

    public AppTheme AppTheme
    {
        get => _appTheme;
        set
        {
            if (SetProperty(ref _appTheme, value))
            {
                ThemeChangeRequested?.Invoke(value);
            }
        }
    }

    public double BarOpacity
    {
        get => _barOpacity;
        set
        {
            if (SetProperty(ref _barOpacity, Math.Clamp(value, 0.3, 1.0)))
            {
                OpacityChangeRequested?.Invoke(_barOpacity);
            }
        }
    }

    public bool UseFixedTabWidth
    {
        get => _useFixedTabWidth;
        set
        {
            if (SetProperty(ref _useFixedTabWidth, value))
            {
                TabWidthChangeRequested?.Invoke(value);
            }
        }
    }

    public bool ShowBranding
    {
        get => _showBranding;
        set
        {
            if (SetProperty(ref _showBranding, value))
            {
                BrandingChangeRequested?.Invoke(value);
            }
        }
    }

    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                LanguageChangeRequested?.Invoke(value);
            }
        }
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
            {
                AccentColorChangeRequested?.Invoke(value);
            }
        }
    }

    public bool AutoHideBar
    {
        get => _autoHideBar;
        set
        {
            if (SetProperty(ref _autoHideBar, value))
            {
                AutoHideChangeRequested?.Invoke(value);
            }
        }
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (SetProperty(ref _launchAtStartup, value))
            {
                LaunchAtStartupChangeRequested?.Invoke(value);
            }
        }
    }

    public bool ShowClock
    {
        get => _showClock;
        set
        {
            if (SetProperty(ref _showClock, value))
            {
                ClockVisibilityChangeRequested?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Current wall-clock time formatted in the user's short-time pattern.
    /// Updated every minute by <see cref="_clockTimer"/>.
    /// </summary>
    public string CurrentTime
    {
        get => _currentTime;
        private set => SetProperty(ref _currentTime, value);
    }

    public BarSize BarSize
    {
        get => _barSize;
        set
        {
            if (SetProperty(ref _barSize, value))
            {
                OnPropertyChanged(nameof(BarHeight));
                OnPropertyChanged(nameof(TabPadding));
                BarSizeChangeRequested?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Enables a system-rendered acrylic blur behind the bar background
    /// (Windows 10 1809+ via SetWindowCompositionAttribute). When off,
    /// the bar uses the regular themed solid background.
    /// </summary>
    public bool UseBlurEffect
    {
        get => _useBlurEffect;
        set
        {
            if (SetProperty(ref _useBlurEffect, value))
            {
                BlurEffectChangeRequested?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Controls whether the app silently checks for new releases at
    /// startup. The user can still trigger a manual check via
    /// <see cref="CheckForUpdatesCommand"/> regardless of this flag.
    /// </summary>
    public bool AutoCheckUpdates
    {
        get => _autoCheckUpdates;
        set
        {
            if (SetProperty(ref _autoCheckUpdates, value))
            {
                AutoCheckUpdatesChangeRequested?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Pixel/DIP height the bar must occupy for the current
    /// <see cref="BarSize"/>. Bound by the view to keep layout in sync
    /// when the user picks a new size.
    /// </summary>
    public double BarHeight => (int)_barSize;

    /// <summary>
    /// Inner padding applied to every tab. Only the left inset scales
    /// with the bar size so larger bars feel proportionally roomier
    /// before the icon, while the right/vertical paddings stay fixed
    /// to preserve the close-button alignment.
    /// Small=8, Medium≈10, Large≈12 left padding.
    /// </summary>
    public System.Windows.Thickness TabPadding => new(BarHeight / 4.5, 4, 6, 4);

    public ICommand SwitchToCommand { get; }
    public ICommand NextTabCommand { get; }
    public ICommand PrevTabCommand { get; }
    public ICommand GoToTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    /// <summary>
    /// Moves the source tab to the position of the target tab.
    /// </summary>
    public void MoveTab(TabViewModel source, TabViewModel target)
    {
        int oldIndex = Tabs.IndexOf(source);
        int newIndex = Tabs.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
        Tabs.Move(oldIndex, newIndex);
    }

    public event Action<bool>? BarPlacementChangeRequested;
    public event Action<bool>? DetectionModeChangeRequested;
    public event Action<AppTheme>? ThemeChangeRequested;
    public event Action<double>? OpacityChangeRequested;
    public event Action<bool>? TabWidthChangeRequested;
    public event Action<bool>? BrandingChangeRequested;
    public event Action<string>? LanguageChangeRequested;
    public event Action<string>? AccentColorChangeRequested;
    public event Action<bool>? AutoHideChangeRequested;
    public event Action<bool>? LaunchAtStartupChangeRequested;
    public event Action<bool>? ClockVisibilityChangeRequested;
    public event Action<BarSize>? BarSizeChangeRequested;
    public event Action<bool>? BlurEffectChangeRequested;
    public event Action<bool>? AutoCheckUpdatesChangeRequested;
    /// <summary>Raised when the user explicitly asks to check for a new release.</summary>
    public event Action? ManualUpdateCheckRequested;

    public MainViewModel(WindowSwitcher switcher, bool startPolling = true)
    {
        _switcher = switcher;

        SwitchToCommand = new RelayCommand(p => SwitchTo(p as TabViewModel));
        NextTabCommand = new RelayCommand(NextTab);
        PrevTabCommand = new RelayCommand(PrevTab);
        GoToTabCommand = new RelayCommand(p => GoToTab(p));
        CloseTabCommand = new RelayCommand(p => CloseTab(p as TabViewModel));
        CheckForUpdatesCommand = new RelayCommand(_ => ManualUpdateCheckRequested?.Invoke());

        _switcher.WindowsChanged += OnWindowsChanged;

        if (startPolling)
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += (_, _) => _switcher.Refresh();
            _pollTimer.Start();
        }

        // Wall clock: tick once per second so the displayed minute flips as
        // soon as the system clock rolls over. The render cost is negligible
        // (a single string allocation only when the formatted value changes
        // through SetProperty), and using a timer aligned to the next minute
        // would still drift on system clock changes / sleep wake-ups.
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        CurrentTime = DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
    }

    public void SetOwnHandle(IntPtr handle)
    {
        _switcher.SetOwnHandle(handle);
        _switcher.Refresh();
    }

    public void Stop()
    {
        _pollTimer?.Stop();
        _clockTimer.Stop();
        _switcher.WindowsChanged -= OnWindowsChanged;
    }

    private void SwitchTo(TabViewModel? tab)
    {
        if (tab is null) return;
        _switcher.SwitchTo(tab.Model);
    }

    private void CloseTab(TabViewModel? tab)
    {
        if (tab is null) return;
        _switcher.CloseWindow(tab.Model);
        Tabs.Remove(tab);
        OnPropertyChanged(nameof(HasTabs));
    }

    private void NextTab()
    {
        if (Tabs.Count < 2) return;
        int activeIdx = -1;
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].IsActive) { activeIdx = i; break; }
        }
        int next = (activeIdx + 1) % Tabs.Count;
        SwitchTo(Tabs[next]);
    }

    private void PrevTab()
    {
        if (Tabs.Count < 2) return;
        int activeIdx = 0;
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].IsActive) { activeIdx = i; break; }
        }
        int prev = (activeIdx - 1 + Tabs.Count) % Tabs.Count;
        SwitchTo(Tabs[prev]);
    }

    private void GoToTab(object? parameter)
    {
        if (parameter is not string s || !int.TryParse(s, out int index)) return;
        index--;
        if (index >= 0 && index < Tabs.Count)
        {
            SwitchTo(Tabs[index]);
        }
    }

    private void OnWindowsChanged()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(SyncTabs);
    }

    private void SyncTabs()
    {
        var all = _switcher.Windows;

        IReadOnlyList<TrackedWindow> current = _monitorFilter is not null
            ? all.Where(w => w.MonitorHandle == _monitorFilter.Value).ToList()
            : all;

        // Two-phase removal to survive transient enumeration drops:
        //
        //   1. Window appears in `current`           → keep / update.
        //   2. Window appears in `all` but not in    → genuinely outside
        //      `current` (monitor filter excluded it)  this bar's scope; remove.
        //   3. Window absent from both AND HWND is   → cloaked transient
        //      still alive                             (display sleep, modern
        //                                              standby, virtual desktop
        //                                              swap). KEEP the tab.
        //   4. Window absent from both AND HWND is   → window was actually
        //      no longer alive                         destroyed; remove.
        //
        // This prevents the bar from emptying out after the user is idle
        // for a while: DWM cloaks every visible window briefly during
        // standby transitions, which used to wipe the entire tab list.
        for (int i = Tabs.Count - 1; i >= 0; i--)
        {
            var handle = Tabs[i].Handle;
            if (current.Any(w => w.Handle == handle)) continue;

            bool inUnfiltered = _monitorFilter is not null && all.Any(w => w.Handle == handle);
            if (inUnfiltered)
            {
                // Window exists but moved off this monitor: drop from this bar.
                Tabs.RemoveAt(i);
                continue;
            }

            if (_switcher.IsWindowAlive(handle))
            {
                // Cloaked / temporarily hidden: keep the tab as-is.
                continue;
            }

            Tabs.RemoveAt(i);
        }

        // Update existing tabs and append new ones at the end (stable order)
        foreach (var win in current)
        {
            var existing = Tabs.FirstOrDefault(t => t.Handle == win.Handle);

            if (existing is not null)
            {
                existing.UpdateFrom(win);
            }
            else
            {
                Tabs.Add(new TabViewModel(win));
            }
        }

        OnPropertyChanged(nameof(HasTabs));
    }
}
