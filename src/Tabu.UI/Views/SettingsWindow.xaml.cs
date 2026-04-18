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
