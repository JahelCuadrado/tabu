using System.Globalization;
using System.Windows;
using FluentAssertions;
using Tabu.UI.Converters;
using Xunit;

namespace Tabu.UI.Tests.Converters;

/// <summary>
/// Locks the contract of the small WPF value converters that the bar's
/// XAML bindings depend on. Wrong outputs here surface as silently
/// invisible elements (notification dots, settings rows), so even
/// trivial converters are worth pinning.
/// </summary>
public sealed class ConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    [InlineData("not-a-bool", Visibility.Collapsed)]
    public void BoolToVisibility_MapsTruthyOnly(object? input, Visibility expected)
    {
        var sut = new BoolToVisibilityConverter();
        sut.Convert(input!, typeof(Visibility), null!, Culture).Should().Be(expected);
    }

    [Fact]
    public void NullToCollapsed_HidesNullObjects()
    {
        var sut = new NullToCollapsedConverter();
        sut.Convert(null, typeof(Visibility), null!, Culture).Should().Be(Visibility.Collapsed);
        sut.Convert(new object(), typeof(Visibility), null!, Culture).Should().Be(Visibility.Visible);
    }

    [Fact]
    public void NotNullToBool_NormalisesNullnessToBoolean()
    {
        var sut = new NotNullToBoolConverter();
        sut.Convert(null, typeof(bool), null!, Culture).Should().Be(false);
        sut.Convert("anything", typeof(bool), null!, Culture).Should().Be(true);
    }

    [Theory]
    [InlineData(true, 200.0)]
    [InlineData(false, double.PositiveInfinity)]
    public void BoolToTabWidth_TogglesBetweenFixedAndAdaptive(bool input, double expected)
    {
        var sut = new BoolToTabWidthConverter();
        sut.Convert(input, typeof(double), null!, Culture).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 200.0)]
    [InlineData(false, 0.0)]
    public void BoolToFixedTabWidth_FeedsTabsPanelDependencyProperty(bool input, double expected)
    {
        var sut = new BoolToFixedTabWidthConverter();
        sut.Convert(input, typeof(double), null!, Culture).Should().Be(expected);
    }

    [Fact]
    public void AllBoolsToVisibility_ReturnsCollapsedWhenAnyInputIsFalsy()
    {
        var sut = new AllBoolsToVisibilityConverter();

        sut.Convert(new object[] { true, true, true }, typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Visible);
        sut.Convert(new object[] { true, false, true }, typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Collapsed);
        sut.Convert(new object[] { true, null!, true }, typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Collapsed);
        sut.Convert(Array.Empty<object>(), typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Collapsed);
    }
}
