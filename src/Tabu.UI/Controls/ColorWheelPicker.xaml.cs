using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tabu.UI.Controls;

/// <summary>
/// Lightweight HSV color wheel: hue is the polar angle, saturation is the
/// radius and value (brightness) is controlled by the side slider. The
/// wheel bitmap is regenerated whenever the brightness changes so the
/// rendered colors always match the current value plane. Designed to
/// avoid third-party dependencies and stay small enough to inline inside
/// a settings panel.
/// </summary>
public partial class ColorWheelPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorWheelPicker),
            new FrameworkPropertyMetadata(
                Colors.Red,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    /// <summary>Fired whenever the user picks a new color via wheel or slider.</summary>
    public event EventHandler<Color>? SelectedColorChanged;

    private const int WheelDiameter = 140;
    private const double WheelRadius = WheelDiameter / 2.0;
    private bool _isDragging;
    private bool _suppressFeedback;
    private double _hue;        // 0..360
    private double _saturation; // 0..1
    private double _value = 1;  // 0..1

    public ColorWheelPicker()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RenderWheel();
            // Initial sync from the bound color.
            SyncFromColor(SelectedColor, suppressEvent: true);
        };
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ColorWheelPicker self || self._suppressFeedback) return;
        self.SyncFromColor((Color)e.NewValue, suppressEvent: true);
    }

    private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        WheelHost.CaptureMouse();
        UpdateFromMouse(e.GetPosition(WheelHost));
    }

    private void Wheel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        WheelHost.ReleaseMouseCapture();
    }

    private void Wheel_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        UpdateFromMouse(e.GetPosition(WheelHost));
    }

    private void Brightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _value = e.NewValue;
        RenderWheel();
        EmitColor();
    }

    private void UpdateFromMouse(Point p)
    {
        var dx = p.X - WheelRadius;
        var dy = p.Y - WheelRadius;
        var r = Math.Sqrt(dx * dx + dy * dy);
        // Clamp to wheel circumference so dragging outside still updates hue.
        var s = Math.Min(r / WheelRadius, 1.0);
        var h = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
        _hue = h;
        _saturation = s;
        UpdateSelectorPosition();
        EmitColor();
    }

    private void UpdateSelectorPosition()
    {
        var radians = _hue * Math.PI / 180.0;
        var radius = _saturation * WheelRadius;
        var x = WheelRadius + Math.Cos(radians) * radius - WheelSelector.Width / 2;
        var y = WheelRadius + Math.Sin(radians) * radius - WheelSelector.Height / 2;
        Canvas.SetLeft(WheelSelector, x);
        Canvas.SetTop(WheelSelector, y);
    }

    private void EmitColor()
    {
        var color = HsvToColor(_hue, _saturation, _value);
        _suppressFeedback = true;
        try
        {
            SelectedColor = color;
        }
        finally
        {
            _suppressFeedback = false;
        }
        SelectedColorChanged?.Invoke(this, color);
    }

    private void SyncFromColor(Color color, bool suppressEvent)
    {
        ColorToHsv(color, out var h, out var s, out var v);
        _hue = h;
        _saturation = s;
        _value = v;

        // Avoid recursive ValueChanged when we tweak the slider here.
        var prevSuppress = _suppressFeedback;
        _suppressFeedback = true;
        try
        {
            BrightnessSlider.Value = v;
        }
        finally
        {
            _suppressFeedback = prevSuppress;
        }

        RenderWheel();
        UpdateSelectorPosition();

        if (!suppressEvent)
        {
            SelectedColorChanged?.Invoke(this, color);
        }
    }

    /// <summary>
    /// Renders the HSV wheel into a <see cref="WriteableBitmap"/> at the
    /// current brightness. The cost is ~25k pixel writes; cheap enough to
    /// run on every brightness tick without a perceivable lag.
    /// </summary>
    private void RenderWheel()
    {
        const int size = WheelDiameter;
        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        var stride = size * 4;
        var pixels = new byte[size * stride];
        var radius = WheelRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - radius;
                double dy = y - radius;
                double r = Math.Sqrt(dx * dx + dy * dy);
                int offset = y * stride + x * 4;

                if (r > radius)
                {
                    // Transparent outside the disc.
                    pixels[offset + 0] = 0;
                    pixels[offset + 1] = 0;
                    pixels[offset + 2] = 0;
                    pixels[offset + 3] = 0;
                    continue;
                }

                double hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
                double saturation = r / radius;
                var c = HsvToColor(hue, saturation, _value);

                pixels[offset + 0] = c.B;
                pixels[offset + 1] = c.G;
                pixels[offset + 2] = c.R;
                pixels[offset + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        WheelImage.Source = bitmap;
    }

    private static Color HsvToColor(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double c = v * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r1 = 0, g1 = 0, b1 = 0;

        switch ((int)Math.Floor(hp))
        {
            case 0: r1 = c; g1 = x; break;
            case 1: r1 = x; g1 = c; break;
            case 2: g1 = c; b1 = x; break;
            case 3: g1 = x; b1 = c; break;
            case 4: r1 = x; b1 = c; break;
            default: r1 = c; b1 = x; break;
        }

        double m = v - c;
        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private static void ColorToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
            return;
        }

        if (max == r)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * ((b - r) / delta + 2);
        }
        else
        {
            h = 60 * ((r - g) / delta + 4);
        }

        if (h < 0) h += 360;
    }
}
