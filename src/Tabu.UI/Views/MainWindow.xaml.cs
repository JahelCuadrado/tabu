using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Tabu.Domain.Entities;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Views;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const int BarHeightPixels = 36;
    private const double DragThreshold = 6.0;

    private IntPtr _hwnd;
    private bool _appBarRegistered;

    // Drag-and-drop state
    private bool _isDragging;
    private Point _dragStartPoint;
    private Border? _dragSource;
    private TabViewModel? _dragTab;

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

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int cx, int cy, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;

    private void RegisterAppBar()
    {
        var abd = NewAppBarData();
        abd.uCallbackMessage = 0; // We don't need callback messages
        SHAppBarMessage(ABM_NEW, ref abd);
        _appBarRegistered = true;
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
        UnregisterAppBar();
        ViewModel.Stop();
    }

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is TabViewModel tab)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragSource = border;
            _dragTab = tab;
            _isDragging = false;
            border.CaptureMouse();
        }
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab is null) return;
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
            if (_dragSource is not null) _dragSource.Opacity = 0.5;
        }

        if (_isDragging)
        {
            var targetBorder = FindTabBorderAtPosition(currentPos);
            if (targetBorder is not null && targetBorder.Tag is TabViewModel targetTab && targetTab != _dragTab)
            {
                ViewModel.MoveTab(_dragTab, targetTab);
                // After Move, the ItemsControl may recreate containers — update _dragSource
                _dragSource = FindTabBorderForTab(_dragTab);
                if (_dragSource is not null) _dragSource.Opacity = 0.5;
            }
        }
    }

    private void Tab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragSource is null) return;

        bool wasDragging = _isDragging;
        var tab = _dragTab;

        CancelDrag();

        // If it was a simple click (no drag), switch to the tab
        if (!wasDragging && tab is not null)
        {
            ViewModel.SwitchToCommand.Execute(tab);
        }
    }

    private void CancelDrag()
    {
        var source = _dragSource;
        _dragSource = null;
        _dragTab = null;
        _isDragging = false;

        if (source is null) return;
        try
        {
            source.ReleaseMouseCapture();
            source.Opacity = 1.0;
            source.Cursor = Cursors.Hand;
        }
        catch
        {
            // Border may have been disconnected from visual tree during reorder
        }
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
            ViewModel.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
