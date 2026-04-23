using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tabu.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToTabWidthConverter : IValueConverter
{
    private const double FixedTabMaxWidth = 200.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? FixedTabMaxWidth : double.PositiveInfinity;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps the <c>UseFixedTabWidth</c> flag to the <c>FixedTabWidth</c>
/// dependency property of <c>TabsPanel</c>: <c>true</c> ⇒ 200 px tabs,
/// <c>false</c> ⇒ 0 (adaptive layout).
/// </summary>
public sealed class BoolToFixedTabWidthConverter : IValueConverter
{
    private const double FixedTabWidthPx = 200.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? FixedTabWidthPx : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns <see cref="Visibility.Visible"/> only when ALL of the supplied
/// boolean inputs are true. Used by the tab template to gate the
/// notification dot on the per-tab <c>HasNotification</c> flag AND the
/// global <c>ShowNotificationBadges</c> user preference.
/// </summary>
public sealed class AllBoolsToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0) return Visibility.Collapsed;
        foreach (var value in values)
        {
            if (value is not true) return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
