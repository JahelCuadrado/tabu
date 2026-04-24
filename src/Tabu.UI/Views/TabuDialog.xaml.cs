using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tabu.UI.Views;

/// <summary>
/// Result returned by <see cref="TabuDialog"/>. Mirrors the subset of
/// <see cref="MessageBoxResult"/> we actually use throughout the app.
/// </summary>
public enum TabuDialogResult
{
    None,
    Yes,
    No,
    Ok,
    Cancel
}

/// <summary>Visual variant for the icon badge in the dialog header.</summary>
public enum TabuDialogVariant
{
    Info,
    Warning,
    Danger,
    Success
}

/// <summary>
/// In-house modal dialog that matches the Tabu visual language: rounded
/// container, accent-coloured icon badge, drop shadow, fade-and-scale
/// entrance. Replaces native Win32 <see cref="MessageBox"/> calls so the
/// app keeps a coherent look across all surfaces.
/// </summary>
public partial class TabuDialog : Window
{
    private TabuDialogResult _result = TabuDialogResult.None;

    private TabuDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Convenience helper that mirrors the most common
    /// <see cref="MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage)"/>
    /// usage. Pre-fills the icon badge based on <paramref name="variant"/>.
    /// </summary>
    public static TabuDialogResult Show(
        Window? owner,
        string message,
        string title,
        TabuDialogVariant variant = TabuDialogVariant.Info,
        string? primaryText = null,
        string? secondaryText = null,
        bool isPrimaryDestructive = false)
    {
        var dialog = new TabuDialog
        {
            Owner = owner
        };
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.ApplyVariant(variant);
        dialog.BuildButtons(primaryText, secondaryText, isPrimaryDestructive);

        dialog.ShowDialog();
        return dialog._result;
    }

    private void ApplyVariant(TabuDialogVariant variant)
    {
        // Glyph + tint for the icon badge. Falls back to AccentBrush so
        // the dialog inherits the user's accent palette in info mode.
        var (glyph, brushKey) = variant switch
        {
            TabuDialogVariant.Warning => ("\uE7BA", "TabuDialog_WarningBrush"),
            TabuDialogVariant.Danger => ("\uE783", "TabuDialog_DangerBrush"),
            TabuDialogVariant.Success => ("\uE73E", "TabuDialog_SuccessBrush"),
            _ => ("\uE946", "AccentBrush")
        };

        IconGlyph.Text = glyph;

        if (TryFindResource(brushKey) is Brush brush)
        {
            IconBadge.Background = brush;
        }
    }

    private void BuildButtons(string? primaryText, string? secondaryText, bool isPrimaryDestructive)
    {
        ButtonsPanel.Children.Clear();

        // Secondary button first (left) when present so the primary stays
        // anchored to the right edge — matches Stripe / Linear / Vercel.
        if (!string.IsNullOrEmpty(secondaryText))
        {
            var secondary = MakeButton(secondaryText, isPrimary: false, isDestructive: false);
            secondary.Margin = new Thickness(0, 0, 8, 0);
            secondary.Click += (_, _) =>
            {
                _result = string.IsNullOrEmpty(primaryText) ? TabuDialogResult.Cancel : TabuDialogResult.No;
                CloseAnimated();
            };
            ButtonsPanel.Children.Add(secondary);
        }

        var primaryLabel = primaryText ?? TryFindResource("Dialog_Ok") as string ?? "OK";
        var primary = MakeButton(primaryLabel, isPrimary: true, isDestructive: isPrimaryDestructive);
        primary.IsDefault = true;
        primary.Click += (_, _) =>
        {
            _result = string.IsNullOrEmpty(secondaryText) ? TabuDialogResult.Ok : TabuDialogResult.Yes;
            CloseAnimated();
        };
        ButtonsPanel.Children.Add(primary);
    }

    private Button MakeButton(string label, bool isPrimary, bool isDestructive)
    {
        var styleKey = isPrimary ? "PrimaryButtonStyle" : "SecondaryButtonStyle";
        var btn = new Button
        {
            Content = label,
            Style = TryFindResource(styleKey) as Style,
            MinWidth = 96,
            Padding = new Thickness(16, 8, 16, 8)
        };

        if (isPrimary && isDestructive && TryFindResource("TabuDialog_DangerBrush") is Brush danger)
        {
            // Override the accent fill so destructive primary buttons
            // (Force kill, Reset settings, etc.) read as warnings.
            btn.Background = danger;
        }

        return btn;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        const int durationMs = 180;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var scale = new DoubleAnimation(0.94, 1, duration) { EasingFunction = ease };

        DialogRoot.BeginAnimation(OpacityProperty, fade);
        if (DialogRoot.RenderTransform is ScaleTransform st)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _result = TabuDialogResult.Cancel;
            CloseAnimated();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _result = TabuDialogResult.Cancel;
        CloseAnimated();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseAnimated()
    {
        const int durationMs = 140;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var fade = new DoubleAnimation(DialogRoot.Opacity, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();
        DialogRoot.BeginAnimation(OpacityProperty, fade);
    }
}
