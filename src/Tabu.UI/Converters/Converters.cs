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
