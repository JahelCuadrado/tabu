using System.Windows;
using System.Windows.Controls;

namespace Tabu.UI.Helpers;

/// <summary>
/// Browser-style horizontal tab layout panel with two operating modes
/// selected via <see cref="FixedTabWidth"/>:
/// <list type="bullet">
///   <item><b>Adaptive (FixedTabWidth ≤ 0):</b> all tabs share the
///         viewport equally, each clamped to
///         <c>[MinTabWidth, MaxTabWidth]</c>. The strip is always
///         100% filled — same as a browser tab bar.</item>
///   <item><b>Fixed (FixedTabWidth &gt; 0):</b> while there is room,
///         every tab keeps the requested fixed width and the leftover
///         space stays empty on the right. Once the total would exceed
///         the viewport, the panel falls back to adaptive uniform
///         compression so tabs shrink instead of overflowing.</item>
/// </list>
/// </summary>
internal sealed class TabsPanel : Panel
{
    public static readonly DependencyProperty MinTabWidthProperty =
        DependencyProperty.Register(
            nameof(MinTabWidth), typeof(double), typeof(TabsPanel),
            new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxTabWidthProperty =
        DependencyProperty.Register(
            nameof(MaxTabWidth), typeof(double), typeof(TabsPanel),
            new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// When greater than zero, switches the panel into "fixed width"
    /// mode (see class summary). A value of 0 means adaptive layout.
    /// </summary>
    public static readonly DependencyProperty FixedTabWidthProperty =
        DependencyProperty.Register(
            nameof(FixedTabWidth), typeof(double), typeof(TabsPanel),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinTabWidth
    {
        get => (double)GetValue(MinTabWidthProperty);
        set => SetValue(MinTabWidthProperty, value);
    }

    public double MaxTabWidth
    {
        get => (double)GetValue(MaxTabWidthProperty);
        set => SetValue(MaxTabWidthProperty, value);
    }

    public double FixedTabWidth
    {
        get => (double)GetValue(FixedTabWidthProperty);
        set => SetValue(FixedTabWidthProperty, value);
    }

    /// <summary>Layout result shared between Measure and Arrange passes.</summary>
    private double _lastTabWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = InternalChildren.Count;
        if (count == 0)
        {
            _lastTabWidth = 0;
            return new Size(0, 0);
        }

        double viewport = double.IsInfinity(availableSize.Width)
            ? MaxTabWidth * count
            : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;

        double tabWidth;
        if (FixedTabWidth > 0 && FixedTabWidth * count <= viewport)
        {
            // Fixed mode with enough room: respect the requested width and
            // leave the trailing space empty.
            tabWidth = FixedTabWidth;
        }
        else if (FixedTabWidth > 0)
        {
            // Fixed mode but overflow: uniformly compress within the same
            // bounds so tabs shrink instead of escaping the viewport.
            tabWidth = Math.Clamp(viewport / count, MinTabWidth, FixedTabWidth);
        }
        else
        {
            // Adaptive mode: always fill the entire viewport. The strip
            // grows freely up to the viewport boundary; only the lower
            // bound matters here so tabs never collapse to zero.
            tabWidth = Math.Max(viewport / count, MinTabWidth);
        }

        _lastTabWidth = tabWidth;

        var childConstraint = new Size(tabWidth, height);
        double maxChildHeight = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childConstraint);
            if (child.DesiredSize.Height > maxChildHeight) maxChildHeight = child.DesiredSize.Height;
        }

        // Cap reported width to the viewport so we never invade the
        // sibling columns (clock, settings, close).
        double total = Math.Min(tabWidth * count, viewport);
        return new Size(total, double.IsInfinity(availableSize.Height) ? maxChildHeight : height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = InternalChildren.Count;
        if (count == 0) return finalSize;

        // Reuse the width computed during Measure; otherwise WPF's slack
        // distribution can stretch each tab to (finalSize.Width / N) and
        // defeat the fixed-width mode.
        double tabWidth = _lastTabWidth > 0 ? _lastTabWidth : finalSize.Width / count;
        tabWidth = Math.Floor(tabWidth);

        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(offset, 0, tabWidth, finalSize.Height));
            offset += tabWidth;
        }

        return finalSize;
    }
}
