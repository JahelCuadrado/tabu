using System.Collections.ObjectModel;
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
    private bool _isBarOnAllMonitors;
    private bool _isDetectSameScreenOnly;
    private AppTheme _appTheme = AppTheme.System;
    private double _barOpacity = 1.0;
    private bool _useFixedTabWidth;
    private IntPtr? _monitorFilter;

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

    public ICommand SwitchToCommand { get; }
    public ICommand NextTabCommand { get; }
    public ICommand PrevTabCommand { get; }
    public ICommand GoToTabCommand { get; }
    public ICommand CloseTabCommand { get; }

    public event Action<bool>? BarPlacementChangeRequested;
    public event Action<bool>? DetectionModeChangeRequested;
    public event Action<AppTheme>? ThemeChangeRequested;
    public event Action<double>? OpacityChangeRequested;
    public event Action<bool>? TabWidthChangeRequested;

    public MainViewModel(WindowSwitcher switcher, bool startPolling = true)
    {
        _switcher = switcher;

        SwitchToCommand = new RelayCommand(p => SwitchTo(p as TabViewModel));
        NextTabCommand = new RelayCommand(NextTab);
        PrevTabCommand = new RelayCommand(PrevTab);
        GoToTabCommand = new RelayCommand(p => GoToTab(p));
        CloseTabCommand = new RelayCommand(p => CloseTab(p as TabViewModel));

        _switcher.WindowsChanged += OnWindowsChanged;

        if (startPolling)
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += (_, _) => _switcher.Refresh();
            _pollTimer.Start();
        }
    }

    public void SetOwnHandle(IntPtr handle)
    {
        _switcher.SetOwnHandle(handle);
        _switcher.Refresh();
    }

    public void Stop()
    {
        _pollTimer?.Stop();
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

        // Remove tabs for windows that no longer exist
        for (int i = Tabs.Count - 1; i >= 0; i--)
        {
            if (!current.Any(w => w.Handle == Tabs[i].Handle))
            {
                Tabs.RemoveAt(i);
            }
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
