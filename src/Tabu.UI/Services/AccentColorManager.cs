using System.Windows;
using System.Windows.Media;
using Tabu.UI.Helpers;

namespace Tabu.UI.Services;

/// <summary>
/// Manages runtime accent color switching by overriding accent and background brushes.
/// Tints backgrounds subtly with the chosen accent color for a cohesive look.
/// </summary>
/// <remarks>
/// Accepts either a legacy preset code (<c>"blue"</c>, <c>"red"</c>, …) for
/// backward compatibility with settings persisted before v1.8.0, or any
/// canonical <c>#RRGGBB</c> hex string emitted by the color-wheel picker.
/// The hover variant is derived from the base color by lightening it in HSL
/// space so the picker no longer needs a pre-computed table.
/// </remarks>
public sealed class AccentColorManager
{
    /// <summary>Default accent code used when input is malformed or empty.</summary>
    public const string DefaultAccentCode = "#3B82F6";

    /// <summary>
    /// Legacy preset map kept ONLY to translate old persisted codes into
    /// hex values. The settings UI no longer exposes these as a closed list;
    /// users can pick any color through the wheel picker.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> LegacyPresets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["purple"] = "#6A42A0",
            ["blue"] = "#3B82F6",
            ["cyan"] = "#06B6D4",
            ["teal"] = "#14B8A6",
            ["green"] = "#22C55E",
            ["yellow"] = "#EAB308",
            ["orange"] = "#F97316",
            ["red"] = "#EF4444",
            ["pink"] = "#EC4899",
            ["rose"] = "#F43F5E"
        };

    private string _currentCode = DefaultAccentCode;

    public string CurrentCode => _currentCode;

    /// <summary>
    /// Resolves the supplied code (legacy preset name or <c>#RRGGBB</c>) into
    /// a canonical hex string and applies it to the application resources.
    /// </summary>
    public void Apply(string colorCode, bool isDarkTheme = true)
    {
        var canonical = ResolveCanonicalHex(colorCode);
        _currentCode = canonical;

        if (!ColorParser.TryParse(canonical, out var accent))
        {
            accent = Colors.DodgerBlue;
        }

        var hover = LightenInHsl(accent, 0.08);

        var resources = System.Windows.Application.Current.Resources;
        resources["AccentColor"] = accent;
        resources["AccentHoverColor"] = hover;
        resources["AccentBrush"] = new SolidColorBrush(accent);
        resources["AccentHoverBrush"] = new SolidColorBrush(hover);

        if (isDarkTheme)
        {
            ApplyDarkTintedBackgrounds(accent, resources);
        }
        else
        {
            ApplyLightTintedBackgrounds(accent, resources);
        }
    }

    public void ReapplyForTheme(bool isDarkTheme)
    {
        Apply(_currentCode, isDarkTheme);
    }

    /// <summary>
    /// Maps the supplied input to a canonical <c>#RRGGBB</c> string. Legacy
    /// preset names are translated; valid hex strings are normalised to
    /// upper-case; everything else falls back to the default accent.
    /// </summary>
    public static string ResolveCanonicalHex(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return DefaultAccentCode;

        var trimmed = input.Trim();

        if (LegacyPresets.TryGetValue(trimmed, out var preset))
        {
            return preset;
        }

        if (TryNormaliseHex(trimmed, out var canonical))
        {
            return canonical;
        }

        return DefaultAccentCode;
    }

    /// <summary>
    /// Accepts <c>#RRGGBB</c> or <c>RRGGBB</c> (with or without leading hash)
    /// and returns the upper-case canonical form. Shorthand <c>#RGB</c> is
    /// rejected on purpose — the picker always emits the full form, and
    /// accepting shorthand would silently allow ambiguous input.
    /// </summary>
    private static bool TryNormaliseHex(string input, out string canonical)
    {
        canonical = string.Empty;
        var v = input.StartsWith('#') ? input[1..] : input;
        if (v.Length != 6) return false;
        for (int i = 0; i < 6; i++)
        {
            if (!Uri.IsHexDigit(v[i])) return false;
        }
        canonical = "#" + v.ToUpperInvariant();
        return true;
    }

    private static void ApplyDarkTintedBackgrounds(Color accent, ResourceDictionary resources)
    {
        var primary = BlendColor(Color.FromRgb(0x18, 0x18, 0x20), accent, 0.06);
        var secondary = BlendColor(Color.FromRgb(0x1C, 0x1C, 0x28), accent, 0.08);
        var tertiary = BlendColor(Color.FromRgb(0x24, 0x24, 0x34), accent, 0.14);
        var border = BlendColor(Color.FromRgb(0x2A, 0x2A, 0x3A), accent, 0.10);
        var borderHover = BlendColor(Color.FromRgb(0x3A, 0x3A, 0x4A), accent, 0.12);

        SetBackgroundResources(resources, primary, secondary, tertiary, border, borderHover);
    }

    private static void ApplyLightTintedBackgrounds(Color accent, ResourceDictionary resources)
    {
        var primary = BlendColor(Color.FromRgb(0xF0, 0xF0, 0xF4), accent, 0.04);
        var secondary = BlendColor(Color.FromRgb(0xE4, 0xE4, 0xEA), accent, 0.05);
        var tertiary = BlendColor(Color.FromRgb(0xD6, 0xD6, 0xDE), accent, 0.08);
        var border = BlendColor(Color.FromRgb(0xC8, 0xC8, 0xD4), accent, 0.06);
        var borderHover = BlendColor(Color.FromRgb(0xB0, 0xB0, 0xC0), accent, 0.08);

        SetBackgroundResources(resources, primary, secondary, tertiary, border, borderHover);
    }

    private static void SetBackgroundResources(
        ResourceDictionary resources,
        Color primary, Color secondary, Color tertiary,
        Color border, Color borderHover)
    {
        resources["PrimaryBackgroundColor"] = primary;
        resources["SecondaryBackgroundColor"] = secondary;
        resources["TertiaryBackgroundColor"] = tertiary;
        resources["BorderColor"] = border;
        resources["BorderHoverColor"] = borderHover;

        resources["PrimaryBackgroundBrush"] = new SolidColorBrush(primary);
        resources["SecondaryBackgroundBrush"] = new SolidColorBrush(secondary);
        resources["TertiaryBackgroundBrush"] = new SolidColorBrush(tertiary);
        resources["BorderBrush"] = new SolidColorBrush(border);
        resources["BorderHoverBrush"] = new SolidColorBrush(borderHover);
    }

    private static Color BlendColor(Color baseColor, Color tint, double amount)
    {
        byte r = (byte)(baseColor.R + (tint.R - baseColor.R) * amount);
        byte g = (byte)(baseColor.G + (tint.G - baseColor.G) * amount);
        byte b = (byte)(baseColor.B + (tint.B - baseColor.B) * amount);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// Derives a hover variant by raising lightness in HSL space. Keeping
    /// the operation in HSL preserves the perceived hue, unlike a naive RGB
    /// blend with white which desaturates the result.
    /// </summary>
    internal static Color LightenInHsl(Color color, double amount)
    {
        RgbToHsl(color, out var h, out var s, out var l);
        l = Math.Clamp(l + amount, 0.0, 1.0);
        return HslToRgb(h, s, l);
    }

    private static void RgbToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;
        if (Math.Abs(max - min) < double.Epsilon)
        {
            h = 0; s = 0;
            return;
        }
        double d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6.0;
        else if (max == g) h = ((b - r) / d + 2) / 6.0;
        else h = ((r - g) / d + 4) / 6.0;
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return Color.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}
