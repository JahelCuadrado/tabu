using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Tabu.UI.Helpers;

/// <summary>
/// Centralised, exception-safe hex color parsing. Replaces the multiple
/// scattered <see cref="ColorConverter.ConvertFromString(string)"/> calls
/// that previously threw on any malformed user input loaded from
/// <c>settings.json</c>.
/// </summary>
public static class ColorParser
{
    /// <summary>
    /// Strict 6-digit hex pattern (<c>#RRGGBB</c>), upper- or lower-case.
    /// Compiled once for hot-path validation in property setters.
    /// </summary>
    private static readonly Regex HexSixPattern =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="value"/> is a strict <c>#RRGGBB</c> string
    /// (no shorthand, no alpha). Whitelisting guard for any setting that
    /// is later persisted to disk.
    /// </summary>
    public static bool IsValidHex(string? value)
        => !string.IsNullOrEmpty(value) && HexSixPattern.IsMatch(value);

    /// <summary>
    /// Safe wrapper around <see cref="ColorConverter.ConvertFromString"/>.
    /// Never throws — returns <c>false</c> for any malformed input.
    /// </summary>
    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(value);
            if (converted is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
            // Malformed hex — caller falls back to a safe default.
        }

        return false;
    }
}
