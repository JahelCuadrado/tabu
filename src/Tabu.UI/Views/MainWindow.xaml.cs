using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Tabu.Domain.Entities;
using Tabu.UI.Services;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Views;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const double DragThreshold = 6.0;
    private const int AutoHideRevealTriggerPixels = 2;
    private const int AutoHidePollIntervalMs = 60;

    /// <summary>
    /// Current bar height in raw pixels. Derived from <see cref="MainViewModel.BarHeight"/>
    /// and used for AppBar reservation, autohide hot-zone math and
    /// <see cref="MoveWindow"/> calls. The VM is the single source of
    /// truth so changes propagate atomically to layout and window sizing.
    /// </summary>
    private int BarHeightPixels => (int)ViewModel.BarHeight;

    private IntPtr _hwnd;
    private bool _appBarRegistered;
    private bool _autoHideEnabled;
    private bool _autoHideRevealed;
    private DispatcherTimer? _autoHideTimer;
    private DispatcherTimer? _fullscreenWatchTimer;
    private bool _hiddenByFullscreen;

    // Drag-and-drop state
    private bool _isDragging;
    private Point _dragStartPoint;
    private Border? _dragSource;
    private TabViewModel? _dragTab;
    private double _dragGrabOffsetX;
    private TranslateTransform? _dragTranslate;
    private int _dragOriginalIdx;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public ScreenInfo? TargetScreen { get; set; }
    public bool IsPrimary { get; set; } = true;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _hwnd = helper.Handle;

        SetToolWindowStyle(_hwnd);
        PositionOnScreen(TargetScreen);
        RegisterAppBar();

        if (!IsPrimary)
        {
            SettingsButton.Visibility = Visibility.Collapsed;
            CloseButton.Visibility = Visibility.Collapsed;
        }

        ViewModel.SetOwnHandle(_hwnd);

        ViewModel.AutoHideChangeRequested += ApplyAutoHide;
        ViewModel.BarSizeChangeRequested += OnBarSizeChanged;
        ViewModel.BlurEffectChangeRequested += ApplyBlurEffect;
        ApplyBlurEffect(ViewModel.UseBlurEffect);
        if (ViewModel.AutoHideBar)
        {
            ApplyAutoHide(true);
        }

        StartFullscreenWatcher();
    }

    /// <summary>
    /// Re-issues the AppBar reservation with the new height so the
    /// taskbar / max-window arrangement honors the freshly chosen
    /// <see cref="BarSize"/>. WPF layout is updated automatically via
    /// the <c>BarHeight</c> binding.
    /// </summary>
    private void OnBarSizeChanged(Tabu.Domain.Entities.BarSize _)
    {
        Dispatcher.BeginInvoke(new Action(SetAppBarPosition), DispatcherPriority.Background);
    }

    private static void SetToolWindowStyle(IntPtr hwnd)
    {
        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);

    #region AppBar Registration

    private const int ABM_NEW = 0x00;
    private const int ABM_REMOVE = 0x01;
    private const int ABM_QUERYPOS = 0x02;
    private const int ABM_SETPOS = 0x03;
    private const int ABE_TOP = 1;

    // AppBar notifications delivered via uCallbackMessage (wParam value).
    private const int ABN_STATECHANGE = 0x0;
    private const int ABN_POSCHANGED = 0x1;
    private const int ABN_FULLSCREENAPP = 0x2;
    private const int ABN_WINDOWARRANGE = 0x3;

    // System-level Win32 messages we observe to keep the reservation fresh.
    private const int WM_DPICHANGED = 0x02E0;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_SETTINGCHANGE = 0x001A;

    private uint _appBarCallbackMessage;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int cx, int cy, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;

    private void RegisterAppBar()
    {
        // Lazily allocate a per-process unique message id so the shell can
        // notify us about reservation changes (taskbar reloc, FS app, etc.).
        if (_appBarCallbackMessage == 0)
        {
            _appBarCallbackMessage = RegisterWindowMessage("TabuAppBarMessage");
        }

        var abd = NewAppBarData();
        abd.uCallbackMessage = _appBarCallbackMessage;
        SHAppBarMessage(ABM_NEW, ref abd);
        _appBarRegistered = true;

        AttachMessageHookIfNeeded();
        SetAppBarPosition();
    }

    private void UnregisterAppBar()
    {
        if (!_appBarRegistered) return;
        var abd = NewAppBarData();
        SHAppBarMessage(ABM_REMOVE, ref abd);
        _appBarRegistered = false;
    }

    private void SetAppBarPosition()
    {
        var abd = NewAppBarData();
        abd.uEdge = ABE_TOP;

        if (TargetScreen is not null)
        {
            abd.rc.Left = TargetScreen.Left;
            abd.rc.Top = TargetScreen.Top;
            abd.rc.Right = TargetScreen.Left + TargetScreen.Width;
            abd.rc.Bottom = TargetScreen.Top + BarHeightPixels;
        }
        else
        {
            abd.rc.Left = 0;
            abd.rc.Top = 0;
            abd.rc.Right = GetSystemMetrics(SM_CXSCREEN);
            abd.rc.Bottom = BarHeightPixels;
        }

        SHAppBarMessage(ABM_QUERYPOS, ref abd);
        SHAppBarMessage(ABM_SETPOS, ref abd);

        // Position window to the reserved area
        MoveWindow(_hwnd, abd.rc.Left, abd.rc.Top,
            abd.rc.Right - abd.rc.Left, abd.rc.Bottom - abd.rc.Top, true);

        // Update WPF layout to match pixel position
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        Left = abd.rc.Left / scaleX;
        Top = abd.rc.Top / scaleY;
        Width = (abd.rc.Right - abd.rc.Left) / scaleX;
        Height = (abd.rc.Bottom - abd.rc.Top) / scaleY;
    }

    private APPBARDATA NewAppBarData()
    {
        return new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = _hwnd
        };
    }

    private bool _messageHookAttached;

    /// <summary>
    /// Subscribes to the Win32 message pump so we can keep the AppBar
    /// reservation in sync with system-wide changes (taskbar relocation,
    /// monitor hot-plug, DPI changes, full-screen apps, theme changes).
    /// Without this, maximized windows can occasionally cover our bar
    /// because Windows holds a stale work-area for that monitor.
    /// </summary>
    private void AttachMessageHookIfNeeded()
    {
        if (_messageHookAttached) return;
        if (HwndSource.FromHwnd(_hwnd) is not HwndSource source) return;
        source.AddHook(WndProc);
        _messageHookAttached = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // AppBar shell notification — wParam holds the ABN_* code.
        if (_appBarCallbackMessage != 0 && msg == (int)_appBarCallbackMessage)
        {
            int notification = wParam.ToInt32();
            if (notification == ABN_POSCHANGED ||
                notification == ABN_WINDOWARRANGE ||
                notification == ABN_STATECHANGE)
            {
                // Re-issue the reservation so Windows picks our top edge again.
                Dispatcher.BeginInvoke(new Action(SetAppBarPosition), DispatcherPriority.Background);
            }
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case WM_DPICHANGED:
            case WM_DISPLAYCHANGE:
            case WM_SETTINGCHANGE:
                Dispatcher.BeginInvoke(new Action(SetAppBarPosition), DispatcherPriority.Background);
                break;
        }

        return IntPtr.Zero;
    }

    #endregion

    public void PositionOnScreen(ScreenInfo? screen)
    {
        if (screen is not null)
        {
            var source = PresentationSource.FromVisual(this);
            double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            Left = screen.WorkLeft / scaleX;
            Top = screen.WorkTop / scaleY;
            Width = screen.WorkWidth / scaleX;
        }
        else
        {
            var workArea = SystemParameters.WorkArea;
            Left = 0;
            Top = 0;
            Width = workArea.Width;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.AutoHideChangeRequested -= ApplyAutoHide;
        ViewModel.BarSizeChangeRequested -= OnBarSizeChanged;
        ViewModel.BlurEffectChangeRequested -= ApplyBlurEffect;
        StopFullscreenWatcher();
        StopAutoHideTimer();
        UnregisterAppBar();
        ViewModel.Stop();
    }

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is TabViewModel tab)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragGrabOffsetX = e.GetPosition(border).X;
            _dragSource = border;
            _dragTab = tab;
            _dragOriginalIdx = ViewModel.Tabs.IndexOf(tab);
            _dragTranslate = EnsureWritableDragTransforms(border);
            _isDragging = false;
            border.CaptureMouse();
        }
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab is null || _dragSource is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelDrag();
            return;
        }

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPoint;

        if (!_isDragging && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
        {
            _isDragging = true;
            Panel.SetZIndex(_dragSource, 1000);
            _dragSource.Cursor = Cursors.SizeAll;
        }

        if (!_isDragging) return;

        // Always force-own the transform on each frame so FluidMove can't override us
        _dragTranslate = EnsureWritableDragTransforms(_dragSource);

        // 1) Make the dragged tab follow the cursor 1:1
        UpdateDragTranslation(currentPos);

        // 2) Detect a sibling tab whose center the cursor crossed and reorder
        TryReorderUnderCursor(currentPos);
    }

    private void TryReorderUnderCursor(Point cursorInWindow)
    {
        if (_dragSource is null || _dragTab is null) return;

        var draggedIdx = ViewModel.Tabs.IndexOf(_dragTab);
        if (draggedIdx < 0) return;

        // Visual center of the dragged tab (where the user is currently dragging it).
        double draggedCenter = cursorInWindow.X - _dragGrabOffsetX + _dragSource.ActualWidth / 2.0;

        // Trigger swap before reaching the neighbor's center: bias each neighbor's
        // effective center toward the dragged tab's ORIGINAL position. The original
        // index is fixed for the whole drag, so this remains a monotonic step
        // function in draggedCenter (no oscillation while dragging slowly).
        const double TriggerOffsetRatio = 0.25;

        int newIndex = 0;
        for (int i = 0; i < ViewModel.Tabs.Count; i++)
        {
            if (i == draggedIdx) continue;

            var border = FindTabBorderForTab(ViewModel.Tabs[i]);
            if (border is null || border.ActualWidth <= 0)
            {
                if (i < draggedIdx) newIndex++;
                continue;
            }

            var slotOriginInWindow = border.TranslatePoint(new Point(0, 0), this);
            double slotLeft = slotOriginInWindow.X;
            if (border.RenderTransform is Transform t && !t.Value.IsIdentity)
            {
                slotLeft -= t.Value.OffsetX;
            }
            double slotCenter = slotLeft + border.ActualWidth / 2.0;

            // For neighbors that started to the LEFT of the original drag position,
            // shift their effective center to the RIGHT (so dragged needs less
            // leftward motion to displace them). Mirror for the other side.
            double effectiveCenter = i < _dragOriginalIdx
                ? slotCenter + border.ActualWidth * TriggerOffsetRatio
                : slotCenter - border.ActualWidth * TriggerOffsetRatio;

            if (effectiveCenter < draggedCenter) newIndex++;
        }

        if (newIndex == draggedIdx) return;

        // Resolve target tab from the desired index and reorder
        var targetTab = ViewModel.Tabs[newIndex];
        ViewModel.MoveTab(_dragTab, targetTab);

        // Same Border instance usually persists (UniformGrid/StackPanel reuse containers).
        // Re-resolve, force layout, then re-translate so the tab sticks to the cursor.
        var newSource = FindTabBorderForTab(_dragTab) ?? _dragSource;
        if (!ReferenceEquals(newSource, _dragSource))
        {
            try { _dragSource.ReleaseMouseCapture(); } catch { }
            Panel.SetZIndex(_dragSource, 0);
            _dragSource = newSource;
            Panel.SetZIndex(_dragSource, 1000);
            _dragSource.CaptureMouse();
        }
        TabsControl.UpdateLayout();
        _dragTranslate = EnsureWritableDragTransforms(_dragSource);
        UpdateDragTranslation(cursorInWindow);
    }

    private void UpdateDragTranslation(Point cursorInWindow)
    {
        if (_dragSource is null || _dragTranslate is null) return;

        // Slot position = where layout placed the tab (without our translate)
        var slotOriginInWindow = _dragSource.TranslatePoint(new Point(0, 0), this);
        var currentTranslate = _dragTranslate.X;
        var slotXNoTranslate = slotOriginInWindow.X - currentTranslate;

        var desiredVisualX = cursorInWindow.X - _dragGrabOffsetX;
        _dragTranslate.X = desiredVisualX - slotXNoTranslate;
    }

    private void Tab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragSource is null) return;

        bool wasDragging = _isDragging;
        var tab = _dragTab;
        var source = _dragSource;
        var translate = _dragTranslate;

        // Reset state immediately so further events don't interfere; the snap animation runs on captured locals
        _dragSource = null;
        _dragTab = null;
        _dragTranslate = null;
        _isDragging = false;

        try { source.ReleaseMouseCapture(); } catch { }
        source.Cursor = Cursors.Hand;

        if (wasDragging)
        {
            AnimateSnapBack(source, translate);
        }
        else if (tab is not null)
        {
            // Simple click: switch to the tab
            ViewModel.SwitchToCommand.Execute(tab);
        }
    }

    private static void AnimateSnapBack(Border source, TranslateTransform? translate)
    {
        if (translate is null || Math.Abs(translate.X) < 0.5)
        {
            Panel.SetZIndex(source, 0);
            if (translate is not null) translate.X = 0;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
            Panel.SetZIndex(source, 0);
        };
        translate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void CancelDrag()
    {
        var source = _dragSource;
        var translate = _dragTranslate;
        _dragSource = null;
        _dragTab = null;
        _dragTranslate = null;
        _isDragging = false;

        if (source is null) return;
        try { source.ReleaseMouseCapture(); } catch { }
        source.Cursor = Cursors.Hand;
        AnimateSnapBack(source, translate);
    }

    private static TranslateTransform? GetTranslateTransform(Border border)
    {
        if (border.RenderTransform is TransformGroup group)
        {
            return GetTranslateInGroup(group);
        }
        return border.RenderTransform as TranslateTransform;
    }

    private static TranslateTransform? GetTranslateInGroup(TransformGroup group)
    {
        foreach (var t in group.Children)
        {
            if (t is TranslateTransform tt) return tt;
        }
        return null;
    }

    /// <summary>
    /// Replaces the Border's RenderTransform with a fresh, writable TransformGroup
    /// containing ScaleTransform + TranslateTransform. This prevents conflicts with
    /// FluidMoveBehavior, which may freeze the existing transform during reorder animations.
    /// </summary>
    private static TranslateTransform EnsureWritableDragTransforms(Border border)
    {
        double scaleX = 1, scaleY = 1, tx = 0, ty = 0;
        if (border.RenderTransform is TransformGroup existing)
        {
            foreach (var t in existing.Children)
            {
                if (t is ScaleTransform st) { scaleX = st.ScaleX; scaleY = st.ScaleY; }
                else if (t is TranslateTransform tt) { tx = tt.X; ty = tt.Y; }
            }
        }
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(scaleX, scaleY));
        var translate = new TranslateTransform(tx, ty);
        group.Children.Add(translate);
        border.RenderTransform = group;
        return translate;
    }

    private Border? FindTabBorderAtPosition(Point position)
    {
        var hit = VisualTreeHelper.HitTest(this, position);
        if (hit?.VisualHit is null) return null;

        DependencyObject current = hit.VisualHit;
        while (current is not null)
        {
            if (current is Border border && border.Tag is TabViewModel)
                return border;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private Border? FindTabBorderForTab(TabViewModel tab)
    {
        var container = TabsControl.ItemContainerGenerator.ContainerFromItem(tab);
        if (container is null) return null;
        return FindChildBorder(container);
    }

    private static Border? FindChildBorder(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border border && border.Tag is TabViewModel)
                return border;
            var result = FindChildBorder(child);
            if (result is not null) return result;
        }
        return null;
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Border border && border.Tag is TabViewModel tab)
        {
            AnimateAndCloseTab(border, tab);
            e.Handled = true;
        }
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not TabViewModel tab) return;

        var border = FindAncestorBorder(button);
        if (border is null)
        {
            ViewModel.CloseTabCommand.Execute(tab);
            return;
        }

        AnimateAndCloseTab(border, tab);
    }

    private void AnimateAndCloseTab(Border tabBorder, TabViewModel tab)
    {
        const int durationMs = 160;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        var shrink = new DoubleAnimation(1, 0.6, duration) { EasingFunction = ease };

        fade.Completed += (_, _) => ViewModel.CloseTabCommand.Execute(tab);

        tabBorder.IsHitTestVisible = false;
        tabBorder.BeginAnimation(UIElement.OpacityProperty, fade);

        var scale = GetScaleTransform(tabBorder);
        if (scale is not null)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }
    }

    private static ScaleTransform? GetScaleTransform(Border border)
    {
        if (border.RenderTransform is TransformGroup group)
        {
            foreach (var t in group.Children)
            {
                if (t is ScaleTransform st) return st;
            }
        }
        return border.RenderTransform as ScaleTransform;
    }

    private static Border? FindAncestorBorder(DependencyObject child)
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is Border border && border.Tag is TabViewModel)
                return border;
        }
        return null;
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Drag disabled when registered as AppBar (fixed position)
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(ViewModel);
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    /// <summary>
    /// The Close button on the primary bar is the user's intent to quit Tabu
    /// entirely. Shutting down the <see cref="System.Windows.Application"/>
    /// guarantees every secondary bar (one per non-primary monitor) is closed
    /// and its AppBar reservation released through <c>App.OnExit</c>.
    /// Closing only this window would leave orphaned bars on other monitors
    /// because WPF's default <c>ShutdownMode</c> is <c>OnLastWindowClose</c>.
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
        => System.Windows.Application.Current.Shutdown();

    #region Auto-Hide

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private void ApplyAutoHide(bool enabled)
    {
        if (_autoHideEnabled == enabled) return;
        _autoHideEnabled = enabled;

        if (enabled)
        {
            // Release the AppBar reservation so the bar can disappear.
            UnregisterAppBar();
            HideBar();
            StartAutoHideTimer();
        }
        else
        {
            StopAutoHideTimer();
            RevealBar();
            // Re-reserve workspace at the top of the screen.
            RegisterAppBar();
        }
    }

    private void StartAutoHideTimer()
    {
        if (_autoHideTimer is not null) return;
        _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(AutoHidePollIntervalMs)
        };
        _autoHideTimer.Tick += AutoHideTimer_Tick;
        _autoHideTimer.Start();
    }

    private void StopAutoHideTimer()
    {
        if (_autoHideTimer is null) return;
        _autoHideTimer.Stop();
        _autoHideTimer.Tick -= AutoHideTimer_Tick;
        _autoHideTimer = null;
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out POINT cursor)) return;

        var (left, top, right, _) = GetTargetScreenBoundsPx();
        bool inHotZone =
            cursor.Y <= top + AutoHideRevealTriggerPixels &&
            cursor.X >= left && cursor.X < right;

        bool overBar = _autoHideRevealed &&
                       cursor.Y >= top && cursor.Y < top + BarHeightPixels &&
                       cursor.X >= left && cursor.X < right;

        if (inHotZone || overBar)
        {
            RevealBar();
        }
        else if (_autoHideRevealed)
        {
            HideBar();
        }
    }

    private (int left, int top, int right, int bottom) GetTargetScreenBoundsPx()
    {
        if (TargetScreen is not null)
        {
            return (TargetScreen.Left, TargetScreen.Top,
                    TargetScreen.Left + TargetScreen.Width,
                    TargetScreen.Top + TargetScreen.Height);
        }

        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        return (0, 0, width, height);
    }

    private const int SM_CYSCREEN = 1;

    private void RevealBar()
    {
        if (_autoHideRevealed) return;
        _autoHideRevealed = true;

        var (left, top, right, _) = GetTargetScreenBoundsPx();
        MoveWindow(_hwnd, left, top, right - left, BarHeightPixels, true);
        Visibility = Visibility.Visible;

        AnimateBarSlide(from: -BarHeightPixels, to: 0, durationMs: 180, EasingMode.EaseOut, onCompleted: null);
    }

    private void HideBar()
    {
        if (!_autoHideRevealed && Visibility == Visibility.Hidden) return;
        _autoHideRevealed = false;

        var (left, top, right, _) = GetTargetScreenBoundsPx();

        AnimateBarSlide(
            from: BarTranslate.Y,
            to: -BarHeightPixels,
            durationMs: 160,
            easing: EasingMode.EaseIn,
            onCompleted: () =>
            {
                MoveWindow(_hwnd, left, top - BarHeightPixels, right - left, BarHeightPixels, true);
                Visibility = Visibility.Hidden;
                BarTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                BarTranslate.Y = 0;
            });
    }

    private void AnimateBarSlide(double from, double to, int durationMs, EasingMode easing, Action? onCompleted)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = easing }
        };

        if (onCompleted is not null)
        {
            anim.Completed += (_, _) => onCompleted();
        }

        BarTranslate.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    #endregion

    #region Acrylic Blur Effect

    /// <summary>
    /// Toggles the system-rendered acrylic backdrop on the bar window.
    /// When enabled the inner background is replaced with a near-zero
    /// alpha brush so the blurred desktop shows through while WPF still
    /// considers every pixel hit-testable; otherwise the transparent
    /// regions between tabs would let mouse clicks pass through to the
    /// desktop and the bar would feel "dead". When disabled we restore
    /// the user-selected <see cref="MainViewModel.BarOpacity"/> binding.
    /// </summary>
    private void ApplyBlurEffect(bool enabled)
    {
        AcrylicWindowEffect.Apply(this, enabled);
        if (BarBackground is null) return;

        if (enabled)
        {
            // Alpha = 1/255 is invisible to the human eye but enough for
            // WPF's hit-test pipeline to treat the brush as opaque, so
            // every click on the bar is captured and forwarded to the
            // tab borders / buttons above. Pure transparent or null
            // brushes let clicks fall through to the underlying window.
            var hitTestBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            hitTestBrush.Freeze();
            BarBackground.SetCurrentValue(System.Windows.Controls.Border.BackgroundProperty, hitTestBrush);
            BarBackground.SetCurrentValue(System.Windows.UIElement.OpacityProperty, 1.0);
        }
        else
        {
            // Restore the original DataBindings (Background + Opacity).
            BarBackground.InvalidateProperty(System.Windows.Controls.Border.BackgroundProperty);
            BarBackground.InvalidateProperty(System.Windows.UIElement.OpacityProperty);
        }
    }

    #endregion

    #region Fullscreen Detection

    private const int FullscreenPollIntervalMs = 400;

    /// <summary>
    /// Starts a low-frequency poll that hides the bar whenever the
    /// foreground window covers the entire monitor (classic fullscreen
    /// games, video players, browsers in F11). Polling is preferred
    /// over <c>SetWinEventHook</c> because it keeps every Win32 callback
    /// on the UI thread and the cost (one GetForegroundWindow + one
    /// rect comparison every 400 ms) is negligible.
    /// </summary>
    private void StartFullscreenWatcher()
    {
        if (_fullscreenWatchTimer is not null) return;
        _fullscreenWatchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(FullscreenPollIntervalMs)
        };
        _fullscreenWatchTimer.Tick += FullscreenWatcher_Tick;
        _fullscreenWatchTimer.Start();
    }

    private void StopFullscreenWatcher()
    {
        if (_fullscreenWatchTimer is null) return;
        _fullscreenWatchTimer.Stop();
        _fullscreenWatchTimer.Tick -= FullscreenWatcher_Tick;
        _fullscreenWatchTimer = null;
    }

    private void FullscreenWatcher_Tick(object? sender, EventArgs e)
    {
        bool fullscreen = IsForegroundFullscreen();

        if (fullscreen && !_hiddenByFullscreen)
        {
            _hiddenByFullscreen = true;
            // Visibility.Hidden keeps the AppBar reservation intact;
            // Collapsed would trigger Windows to reflow maximized
            // windows. We only want to disappear visually and stop
            // intercepting clicks.
            Visibility = Visibility.Hidden;
        }
        else if (!fullscreen && _hiddenByFullscreen)
        {
            _hiddenByFullscreen = false;
            // Skip restoration if auto-hide currently wants the bar
            // hidden — it owns the visibility in that mode.
            if (!_autoHideEnabled || _autoHideRevealed)
            {
                Visibility = Visibility.Visible;
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the active foreground window matches the
    /// monitor bounds it lives on AND that monitor is the same one this
    /// bar instance is anchored to. Filtering by monitor is essential
    /// in multi-monitor setups: a fullscreen game on monitor #1 must
    /// never hide the bar reserved on monitor #2.
    /// </summary>
    private bool IsForegroundFullscreen()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _hwnd) return false;

        // Ignore the shell desktop/taskbar; otherwise we'd hide on the desktop.
        IntPtr shell = GetShellWindow();
        IntPtr desktop = GetDesktopWindow();
        if (fg == shell || fg == desktop) return false;

        if (!GetWindowRect(fg, out var winRect)) return false;

        IntPtr fgMonitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        if (fgMonitor == IntPtr.Zero) return false;

        // Restrict the hide action to the bar that lives on the same
        // monitor as the fullscreen window. Other bars stay visible.
        IntPtr ownMonitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        if (ownMonitor != IntPtr.Zero && ownMonitor != fgMonitor) return false;

        var monInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(fgMonitor, ref monInfo)) return false;

        // Use full monitor bounds (rcMonitor), not the work area: a
        // fullscreen app covers the whole screen including any AppBar.
        var mr = monInfo.rcMonitor;
        return winRect.Left <= mr.Left && winRect.Top <= mr.Top &&
               winRect.Right >= mr.Right && winRect.Bottom >= mr.Bottom;
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECTL rcMonitor;
        public RECTL rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECTL { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECTL lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}
