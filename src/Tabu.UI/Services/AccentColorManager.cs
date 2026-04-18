using System.Windows;
using System.Windows.Media;

namespace Tabu.UI.Services;

/// <summary>
/// Manages runtime accent color switching by overriding accent and background brushes.
/// Tints backgrounds subtly with the chosen accent color for a cohesive look.
/// </summary>
public sealed class AccentColorManager
{
    public static readonly IReadOnlyList<AccentColorOption> AvailableColors = new List<AccentColorOption>
    {
        new("purple", "Purple", "#533483", "#6A42A0"),
        new("blue", "Blue", "#2563EB", "#3B82F6"),
        new("cyan", "Cyan", "#0891B2", "#06B6D4"),
        new("teal", "Teal", "#0D9488", "#14B8A6"),
        new("green", "Green", "#16A34A", "#22C55E"),
        new("yellow", "Yellow", "#CA8A04", "#EAB308"),
        new("orange", "Orange", "#EA580C", "#F97316"),
        new("red", "Red", "#DC2626", "#EF4444"),
        new("pink", "Pink", "#DB2777", "#EC4899"),
        new("rose", "Rose", "#E11D48", "#F43F5E")
    };

    private string _currentCode = "purple";

    public string CurrentCode => _currentCode;

    public void Apply(string colorCode, bool isDarkTheme = true)
    {
        var option = AvailableColors.FirstOrDefault(c => c.Code == colorCode)
                     ?? AvailableColors[0];

        _currentCode = option.Code;

        var resources = System.Windows.Application.Current.Resources;

        var accent = (Color)ColorConverter.ConvertFromString(option.Hex);
        var hover = (Color)ColorConverter.ConvertFromString(option.HoverHex);

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
}

public sealed record AccentColorOption(string Code, string DisplayName, string Hex, string HoverHex)
{
    public override string ToString() => DisplayName;
}
