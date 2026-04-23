using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tabu.UI.Services;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _initialized;

    public SettingsWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;

        // Sync radio buttons with current state
        if (_viewModel.IsBarOnAllMonitors)
        {
            AllBarsRadio.IsChecked = true;
        }
        else
        {
            PrimaryBarRadio.IsChecked = true;
        }

        if (_viewModel.IsDetectSameScreenOnly)
        {
            DetectSameScreenRadio.IsChecked = true;
        }
        else
        {
            DetectAllRadio.IsChecked = true;
        }

        switch (_viewModel.AppTheme)
        {
            case AppTheme.Dark:
                ThemeDarkRadio.IsChecked = true;
                break;
            case AppTheme.Light:
                ThemeLightRadio.IsChecked = true;
                break;
            default:
                ThemeSystemRadio.IsChecked = true;
                break;
        }

        OpacitySlider.Value = _viewModel.BarOpacity * 100;
        OpacityValueText.Text = $"{(int)(_viewModel.BarOpacity * 100)}%";

        if (_viewModel.UseFixedTabWidth)
        {
            TabFixedWidthRadio.IsChecked = true;
        }
        else
        {
            TabFullWidthRadio.IsChecked = true;
        }

        if (_viewModel.ShowBranding)
        {
            BrandingVisibleRadio.IsChecked = true;
        }
        else
        {
            BrandingHiddenRadio.IsChecked = true;
        }

        // Language combo
        LanguageCombo.ItemsSource = LocalizationManager.AvailableLanguages;
        var currentLang = LocalizationManager.AvailableLanguages
            .FirstOrDefault(l => l.Code == _viewModel.Language)
            ?? LocalizationManager.AvailableLanguages[0];
        LanguageCombo.SelectedItem = currentLang;

        // Accent color combo
        AccentColorCombo.ItemsSource = AccentColorManager.AvailableColors;
        var currentAccent = AccentColorManager.AvailableColors
            .FirstOrDefault(c => c.Code == _viewModel.AccentColor)
            ?? AccentColorManager.AvailableColors[0];
        AccentColorCombo.SelectedItem = currentAccent;

        if (_viewModel.AutoHideBar)
        {
            BarAutoHideRadio.IsChecked = true;
        }
        else
        {
            BarAlwaysVisibleRadio.IsChecked = true;
        }

        if (_viewModel.LaunchAtStartup)
        {
            StartupEnabledRadio.IsChecked = true;
        }
        else
        {
            StartupDisabledRadio.IsChecked = true;
        }

        if (_viewModel.ShowClock)
        {
            ClockVisibleRadio.IsChecked = true;
        }
        else
        {
            ClockHiddenRadio.IsChecked = true;
        }

        switch (_viewModel.ClockSize)
        {
            case Tabu.Domain.Entities.ClockSize.Medium:
                ClockSizeMediumRadio.IsChecked = true;
                break;
            case Tabu.Domain.Entities.ClockSize.Large:
                ClockSizeLargeRadio.IsChecked = true;
                break;
            default:
                ClockSizeSmallRadio.IsChecked = true;
                break;
        }
        UpdateClockSizeAvailability();

        if (_viewModel.ShowNotificationBadges)
        {
            NotificationsEnabledRadio.IsChecked = true;
        }
        else
        {
            NotificationsDisabledRadio.IsChecked = true;
        }

        NotificationDotSizeSlider.Value = _viewModel.NotificationDotSize;
        NotificationDotSizeValueText.Text = $"{(int)_viewModel.NotificationDotSize} px";
        NotificationDotColorBox.Text = _viewModel.NotificationDotColor;
        SyncWheelFromHex(_viewModel.NotificationDotColor);
        UpdateNotificationDotPanelAvailability();

        switch (_viewModel.BarSize)
        {
            case Tabu.Domain.Entities.BarSize.Medium:
                BarSizeMediumRadio.IsChecked = true;
                break;
            case Tabu.Domain.Entities.BarSize.Large:
                BarSizeLargeRadio.IsChecked = true;
                break;
            default:
                BarSizeSmallRadio.IsChecked = true;
                break;
        }

        if (_viewModel.UseBlurEffect)
        {
            BlurEnabledRadio.IsChecked = true;
        }
        else
        {
            BlurDisabledRadio.IsChecked = true;
        }

        UpdateThemeLockState();

        if (_viewModel.AutoCheckUpdates)
        {
            AutoUpdatesEnabledRadio.IsChecked = true;
        }
        else
        {
            AutoUpdatesDisabledRadio.IsChecked = true;
        }

        _initialized = true;
    }

    private void BarPlacement_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool allMonitors = AllBarsRadio.IsChecked == true;

        if (allMonitors != _viewModel.IsBarOnAllMonitors)
        {
            _viewModel.IsBarOnAllMonitors = allMonitors;
        }
    }

    private void DetectionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool sameScreen = DetectSameScreenRadio.IsChecked == true;

        if (sameScreen != _viewModel.IsDetectSameScreenOnly)
        {
            _viewModel.IsDetectSameScreenOnly = sameScreen;
        }
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        AppTheme theme;
        if (ThemeDarkRadio.IsChecked == true)
            theme = AppTheme.Dark;
        else if (ThemeLightRadio.IsChecked == true)
            theme = AppTheme.Light;
        else
            theme = AppTheme.System;

        if (theme != _viewModel.AppTheme)
        {
            _viewModel.AppTheme = theme;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;

        double opacity = e.NewValue / 100.0;
        _viewModel.BarOpacity = opacity;
        OpacityValueText.Text = $"{(int)e.NewValue}%";
    }

    private void TabWidth_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool fixedWidth = TabFixedWidthRadio.IsChecked == true;

        if (fixedWidth != _viewModel.UseFixedTabWidth)
        {
            _viewModel.UseFixedTabWidth = fixedWidth;
        }
    }

    private void Branding_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = BrandingVisibleRadio.IsChecked == true;

        if (show != _viewModel.ShowBranding)
        {
            _viewModel.ShowBranding = show;
        }
    }

    private void Language_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (LanguageCombo.SelectedItem is LanguageOption selected && selected.Code != _viewModel.Language)
        {
            _viewModel.Language = selected.Code;
        }
    }

    private void AccentColor_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        if (AccentColorCombo.SelectedItem is AccentColorOption selected && selected.Code != _viewModel.AccentColor)
        {
            _viewModel.AccentColor = selected.Code;
        }
    }

    private void BarVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool autoHide = BarAutoHideRadio.IsChecked == true;

        if (autoHide != _viewModel.AutoHideBar)
        {
            _viewModel.AutoHideBar = autoHide;
        }
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = StartupEnabledRadio.IsChecked == true;

        if (enabled != _viewModel.LaunchAtStartup)
        {
            _viewModel.LaunchAtStartup = enabled;
        }
    }

    private void Clock_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = ClockVisibleRadio.IsChecked == true;

        if (show != _viewModel.ShowClock)
        {
            _viewModel.ShowClock = show;
        }
        UpdateClockSizeAvailability();
    }

    private void ClockSize_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        Tabu.Domain.Entities.ClockSize size;
        if (ClockSizeMediumRadio.IsChecked == true)
            size = Tabu.Domain.Entities.ClockSize.Medium;
        else if (ClockSizeLargeRadio.IsChecked == true)
            size = Tabu.Domain.Entities.ClockSize.Large;
        else
            size = Tabu.Domain.Entities.ClockSize.Small;

        if (size != _viewModel.ClockSize)
        {
            _viewModel.ClockSize = size;
        }
    }

    /// <summary>
    /// Collapses the clock-size selector when the clock itself is hidden,
    /// keeping the settings UI free of dead controls.
    /// </summary>
    private void UpdateClockSizeAvailability()
    {
        if (ClockSizePanel is null) return;
        ClockSizePanel.Visibility = _viewModel.ShowClock ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Notifications_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool show = NotificationsEnabledRadio.IsChecked == true;
        if (show != _viewModel.ShowNotificationBadges)
        {
            _viewModel.ShowNotificationBadges = show;
        }
        UpdateNotificationDotPanelAvailability();
    }

    private void NotificationDotSize_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;

        var size = (int)Math.Round(e.NewValue);
        if (NotificationDotSizeValueText is not null)
        {
            NotificationDotSizeValueText.Text = $"{size} px";
        }
        if (Math.Abs(_viewModel.NotificationDotSize - size) > 0.0001)
        {
            _viewModel.NotificationDotSize = size;
        }
    }

    private void NotificationDotColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;

        var raw = NotificationDotColorBox.Text?.Trim() ?? string.Empty;
        // Empty input → follow accent. Otherwise only push valid #RRGGBB so
        // half-typed values don’t flicker the bar mid-edit.
        if (raw.Length == 0)
        {
            if (_viewModel.NotificationDotColor.Length != 0)
            {
                _viewModel.NotificationDotColor = string.Empty;
            }
            return;
        }

        if (TryNormaliseHex(raw, out var canonical) && !string.Equals(canonical, _viewModel.NotificationDotColor, StringComparison.Ordinal))
        {
            _viewModel.NotificationDotColor = canonical;
            SyncWheelFromHex(canonical);
        }
    }

    private void NotificationDotColorReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        NotificationDotColorBox.Text = string.Empty;
        if (_viewModel.NotificationDotColor.Length != 0)
        {
            _viewModel.NotificationDotColor = string.Empty;
        }
    }

    private void DotColorWheel_SelectedColorChanged(object? sender, System.Windows.Media.Color color)
    {
        if (!_initialized) return;
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (!string.Equals(NotificationDotColorBox.Text, hex, StringComparison.OrdinalIgnoreCase))
        {
            // Setting the textbox triggers TextChanged, which pushes the
            // value to the VM and avoids duplicating the persistence path.
            NotificationDotColorBox.Text = hex;
        }
    }

    private void DotColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (DotColorPopup is null) return;
        // Re-sync the wheel to the current value just before showing it
        // so the selector lands on the right hue/saturation/brightness.
        SyncWheelFromHex(_viewModel.NotificationDotColor);
        DotColorPopup.IsOpen = !DotColorPopup.IsOpen;
    }

    /// <summary>
    /// Pushes a hex string into the wheel without retriggering its event
    /// loop. Falls back to the current accent color when <paramref name="hex"/>
    /// is empty so the wheel always shows a meaningful selector position.
    /// </summary>
    private void SyncWheelFromHex(string hex)
    {
        if (DotColorWheel is null) return;
        System.Windows.Media.Color color;
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return;
            }
        }
        else if (TryFindResource("AccentColor") is System.Windows.Media.Color accent)
        {
            color = accent;
        }
        else
        {
            color = System.Windows.Media.Colors.DodgerBlue;
        }
        DotColorWheel.SelectedColor = color;
    }

    /// <summary>
    /// Hides the dot size/color editor when notifications are disabled, so
    /// the user can’t tweak invisible UI.
    /// </summary>
    private void UpdateNotificationDotPanelAvailability()
    {
        if (NotificationDotPanel is null) return;
        NotificationDotPanel.Visibility = _viewModel.ShowNotificationBadges ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Accepts <c>#RRGGBB</c>, <c>RRGGBB</c>, <c>#RGB</c> or <c>RGB</c> and
    /// returns the canonical upper-case <c>#RRGGBB</c> form. Returns false
    /// for any malformed input so the caller can ignore in-flight typing.
    /// </summary>
    private static bool TryNormaliseHex(string input, out string canonical)
    {
        canonical = string.Empty;
        var v = input.StartsWith('#') ? input[1..] : input;
        if (v.Length == 3)
        {
            v = $"{v[0]}{v[0]}{v[1]}{v[1]}{v[2]}{v[2]}";
        }
        if (v.Length != 6) return false;
        for (int i = 0; i < 6; i++)
        {
            if (!Uri.IsHexDigit(v[i])) return false;
        }
        canonical = "#" + v.ToUpperInvariant();
        return true;
    }

    private void BarSize_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        Tabu.Domain.Entities.BarSize size;
        if (BarSizeMediumRadio.IsChecked == true)
            size = Tabu.Domain.Entities.BarSize.Medium;
        else if (BarSizeLargeRadio.IsChecked == true)
            size = Tabu.Domain.Entities.BarSize.Large;
        else
            size = Tabu.Domain.Entities.BarSize.Small;

        if (size != _viewModel.BarSize)
        {
            _viewModel.BarSize = size;
        }
    }

    private void Blur_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = BlurEnabledRadio.IsChecked == true;
        if (enabled != _viewModel.UseBlurEffect)
        {
            _viewModel.UseBlurEffect = enabled;
        }

        // App.xaml.cs forces the theme to Dark when blur turns on; mirror
        // that change in the settings UI and grey out the radios so the
        // user cannot pick an incompatible combination.
        if (enabled)
        {
            ThemeDarkRadio.IsChecked = true;
        }
        UpdateThemeLockState();
    }

    /// <summary>
    /// Soft-locks the theme & opacity controls when blur is on. The
    /// underlying inputs stay enabled (so the bound values still display
    /// correctly), but a transparent overlay intercepts clicks and the
    /// section is faded to communicate the disabled state. Attempting
    /// to interact triggers a toast explaining why.
    /// </summary>
    private void UpdateThemeLockState()
    {
        bool locked = _viewModel.UseBlurEffect;
        double targetOpacity = locked ? 0.4 : 1.0;
        Visibility overlayVisibility = locked ? Visibility.Visible : Visibility.Collapsed;

        ThemeContent.Opacity = targetOpacity;
        ThemeLockOverlay.Visibility = overlayVisibility;

        OpacityContent.Opacity = targetOpacity;
        OpacityLockOverlay.Visibility = overlayVisibility;
    }

    private void LockedSection_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // The overlay only exists while blur is active, so we can show
        // the toast unconditionally. Mark handled to prevent the click
        // from reaching the underlying control.
        e.Handled = true;
        ShowToast(TryFindResource("Settings_BlurLockedHint") as string
            ?? "This option is disabled while background blur is active.");
    }

    private System.Windows.Threading.DispatcherTimer? _toastDismissTimer;

    /// <summary>
    /// Slides a small banner up from the bottom of the settings window
    /// and fades it out after a short delay. Reusing the same banner
    /// keeps the UI snappy if the user repeatedly clicks a locked area.
    /// </summary>
    private void ShowToast(string message)
    {
        ToastText.Text = message;

        // Reset any in-flight animation/timer so consecutive clicks restart
        // the toast cleanly rather than fighting an ongoing fade-out.
        ToastBanner.BeginAnimation(OpacityProperty, null);
        if (ToastBanner.RenderTransform is TranslateTransform tt)
        {
            tt.BeginAnimation(TranslateTransform.YProperty, null);
            tt.Y = 20;
        }
        _toastDismissTimer?.Stop();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = System.TimeSpan.FromMilliseconds(200);

        ToastBanner.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

        if (ToastBanner.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(20, 0, duration) { EasingFunction = ease });
        }

        _toastDismissTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(2200)
        };
        _toastDismissTimer.Tick += (_, _) =>
        {
            _toastDismissTimer?.Stop();
            var fadeOut = new DoubleAnimation(1, 0, System.TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            ToastBanner.BeginAnimation(OpacityProperty, fadeOut);
        };
        _toastDismissTimer.Start();
    }

    private void AutoUpdates_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        bool enabled = AutoUpdatesEnabledRadio.IsChecked == true;
        if (enabled != _viewModel.AutoCheckUpdates)
        {
            _viewModel.AutoCheckUpdates = enabled;
        }
    }

    private void CheckUpdatesNow_Click(object sender, RoutedEventArgs e)
    {
        // Forwards to the VM command which raises ManualUpdateCheckRequested
        // — App.xaml.cs handles the actual orchestration so this view
        // remains free of update domain knowledge.
        if (_viewModel.CheckForUpdatesCommand.CanExecute(null))
        {
            _viewModel.CheckForUpdatesCommand.Execute(null);
        }
    }

    /// <summary>
    /// Restores every Tabu setting to its factory default. Mirrors
    /// the values declared in <see cref="Tabu.Domain.Entities.UserSettings"/>.
    /// Each radio/slider/combo assignment fires its existing
    /// <c>*_Changed</c> handler, so the MainViewModel is updated and
    /// persisted exactly as if the user had toggled each control by hand.
    /// </summary>
    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var title = TryFindResource("Settings_ResetConfirmTitle") as string ?? "Reset settings";
        var body = TryFindResource("Settings_ResetConfirmBody") as string
                   ?? "All Tabu settings will be restored to their default values. Continue?";

        var result = MessageBox.Show(
            this,
            body,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // Bar layout
        AllBarsRadio.IsChecked = true;
        DetectSameScreenRadio.IsChecked = true;
        BarSizeSmallRadio.IsChecked = true;
        BarAlwaysVisibleRadio.IsChecked = true;

        // Appearance
        BlurDisabledRadio.IsChecked = true;
        ThemeSystemRadio.IsChecked = true;
        OpacitySlider.Value = 100;
        var defaultAccent = AccentColorManager.AvailableColors
            .FirstOrDefault(c => c.Code == "blue")
            ?? AccentColorManager.AvailableColors[0];
        AccentColorCombo.SelectedItem = defaultAccent;
        BrandingVisibleRadio.IsChecked = true;

        // Tabs
        TabFixedWidthRadio.IsChecked = true;
        ClockVisibleRadio.IsChecked = true;
        ClockSizeSmallRadio.IsChecked = true;
        UpdateClockSizeAvailability();
        NotificationsEnabledRadio.IsChecked = true;
        NotificationDotSizeSlider.Value = 7;
        NotificationDotColorBox.Text = string.Empty;
        SyncWheelFromHex(string.Empty);
        UpdateNotificationDotPanelAvailability();

        // System
        var defaultLanguage = LocalizationManager.AvailableLanguages
            .FirstOrDefault(l => l.Code == "en")
            ?? LocalizationManager.AvailableLanguages[0];
        LanguageCombo.SelectedItem = defaultLanguage;
        StartupDisabledRadio.IsChecked = true;
        AutoUpdatesEnabledRadio.IsChecked = true;

        ShowToast(TryFindResource("Settings_ResetSuccess") as string ?? "Settings restored to defaults.");
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        const int durationMs = 220;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = System.TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var grow = new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease };

        SettingsRoot.BeginAnimation(OpacityProperty, fade);

        if (SettingsRoot.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }
}
