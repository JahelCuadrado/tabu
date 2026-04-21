namespace Tabu.Application.Services;

/// <summary>
/// Concrete backdrop effect to apply on the bar window.
/// </summary>
/// <remarks>
/// The values map directly to <c>SetWindowCompositionAttribute</c>'s
/// <c>AccentState</c> enumeration so the UI layer never has to translate
/// from a string-based user preference to the Win32 surface.
/// </remarks>
public enum BackdropMode
{
    /// <summary>No backdrop. Background renders with the user opacity.</summary>
    Disabled = 0,

    /// <summary>
    /// Fluent acrylic blur (<c>ACCENT_ENABLE_ACRYLICBLURBEHIND</c>).
    /// Highest visual quality; can be silently disabled by Windows 11
    /// Enterprise security baselines or legacy GPOs.
    /// </summary>
    Acrylic = 1,

    /// <summary>
    /// Aero gaussian blur (<c>ACCENT_ENABLE_BLURBEHIND</c>). Lower
    /// fidelity than acrylic but reliably available on every Windows
    /// 10 1809+ host because it is rendered by the kernel-mode
    /// composition stack and is not gated by the same policies.
    /// </summary>
    GaussianBlur = 2,
}

/// <summary>
/// Pure resolver that converts the user preference plus the host OS
/// build number into a single concrete <see cref="BackdropMode"/>.
/// Living in the Application layer keeps the decision unit-testable
/// and isolates the UI from the parsing rules.
/// </summary>
public static class BackdropPolicy
{
    /// <summary>
    /// Build number of the first Windows release that supports the
    /// composition-attribute acrylic effect (Windows 10 1809).
    /// </summary>
    public const int MinimumAcrylicBuild = 17763;

    /// <summary>
    /// Build number of the first Windows release that supports the
    /// composition-attribute gaussian blur (Windows 10 1809). Older
    /// builds shipped Aero but exposed it through a different API.
    /// </summary>
    public const int MinimumGaussianBlurBuild = 17763;

    /// <summary>
    /// Resolve the effective backdrop for the running host.
    /// </summary>
    /// <param name="userEnabled">
    /// User-level master toggle. When <c>false</c> the bar always uses
    /// the opaque background regardless of the requested mode.
    /// </param>
    /// <param name="requestedMode">
    /// Raw string from <c>UserSettings.BlurMode</c>. Case-insensitive.
    /// Unknown values fall back to <see cref="BackdropMode.Acrylic"/>
    /// so existing installs preserve their previous behaviour.
    /// </param>
    /// <param name="osBuildNumber">
    /// <c>Environment.OSVersion.Version.Build</c> from the caller.
    /// Tests inject arbitrary values to exercise every branch.
    /// </param>
    public static BackdropMode Resolve(bool userEnabled, string? requestedMode, int osBuildNumber)
    {
        if (!userEnabled)
        {
            return BackdropMode.Disabled;
        }

        var parsed = ParseMode(requestedMode);

        return parsed switch
        {
            BackdropMode.Acrylic when osBuildNumber >= MinimumAcrylicBuild => BackdropMode.Acrylic,
            BackdropMode.GaussianBlur when osBuildNumber >= MinimumGaussianBlurBuild => BackdropMode.GaussianBlur,
            BackdropMode.Disabled => BackdropMode.Disabled,
            _ => BackdropMode.Disabled,
        };
    }

    /// <summary>
    /// Parse the persisted preference string into the typed enum. Used
    /// directly by the settings UI so the same lookup runs everywhere.
    /// </summary>
    public static BackdropMode ParseMode(string? requestedMode)
    {
        if (string.IsNullOrWhiteSpace(requestedMode))
        {
            return BackdropMode.Acrylic;
        }

        return requestedMode.Trim().ToLowerInvariant() switch
        {
            "acrylic" => BackdropMode.Acrylic,
            "gaussian" or "gaussianblur" or "blur" => BackdropMode.GaussianBlur,
            "disabled" or "off" or "none" => BackdropMode.Disabled,
            _ => BackdropMode.Acrylic,
        };
    }
}
