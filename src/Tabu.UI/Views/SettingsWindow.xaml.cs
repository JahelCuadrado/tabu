using System.Windows;
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

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
