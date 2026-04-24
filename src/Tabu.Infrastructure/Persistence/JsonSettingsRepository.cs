using System.Text.Json;
using System.Text.RegularExpressions;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Infrastructure.Persistence;

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private const string SettingsFileName = "settings.json";

    /// <summary>
    /// Synchronises concurrent <see cref="Save"/> calls. The view-model
    /// fires multiple property-changed events in rapid succession when the
    /// user drags a slider; each one schedules a <see cref="Task.Run"/>
    /// save. Without serialising the writes, two threads can race on the
    /// same temp-file rename and corrupt the on-disk JSON.
    /// </summary>
    private static readonly object SaveLock = new();

    private static readonly Regex HexColorPattern =
        new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedThemes =
        new(StringComparer.OrdinalIgnoreCase) { "System", "Light", "Dark" };

    private static readonly HashSet<string> AllowedBarSizes =
        new(StringComparer.OrdinalIgnoreCase) { "Small", "Medium", "Large" };

    private static readonly HashSet<string> AllowedClockSizes =
        new(StringComparer.OrdinalIgnoreCase) { "Small", "Medium", "Large" };

    private static readonly HashSet<string> AllowedBlurModes =
        new(StringComparer.OrdinalIgnoreCase) { "Acrylic", "Gaussian", "Disabled" };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// Production constructor: persists settings under
    /// <c>%LOCALAPPDATA%\Tabu\settings.json</c>.
    /// </summary>
    public JsonSettingsRepository()
        : this(DefaultFilePath()) { }

    /// <summary>
    /// Test/advanced constructor: persists settings at the supplied
    /// path. The parent directory is created lazily on save.
    /// </summary>
    public JsonSettingsRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Resolved storage path used by this repository instance. Exposed
    /// for diagnostics and tests; not part of the persistence contract.
    /// </summary>
    public string FilePath => _filePath;

    public UserSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions)
                           ?? new UserSettings();
            return Sanitise(settings);
        }
        catch
        {
            // Corrupt file: fall back to defaults rather than crashing
            // on startup. The next Save() call rewrites the file with a
            // valid payload.
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sanitised = Sanitise(settings);
        var json = JsonSerializer.Serialize(sanitised, SerializerOptions);

        // Atomic write: serialise to a sibling temp file and use
        // File.Replace so a process crash mid-write can never leave the
        // user with an empty or truncated settings.json. The lock
        // prevents two concurrent Save() calls from racing each other.
        lock (SaveLock)
        {
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(_filePath))
            {
                // File.Replace performs an atomic rename on NTFS and also
                // preserves the destination's ACLs, which matters for
                // roaming-profile environments.
                File.Replace(tempPath, _filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, _filePath);
            }
        }
    }

    /// <summary>
    /// Defensive whitelisting against a settings.json that may have been
    /// hand-edited or corrupted. Out-of-range numerics are clamped to the
    /// safe envelope; unknown enum strings collapse to documented defaults.
    /// Mirrors the validation performed by the view-model setters so the
    /// in-memory model never observes an invalid state.
    /// </summary>
    internal static UserSettings Sanitise(UserSettings raw)
    {
        return new UserSettings
        {
            IsBarOnAllMonitors = raw.IsBarOnAllMonitors,
            IsDetectSameScreenOnly = raw.IsDetectSameScreenOnly,
            AppTheme = AllowedThemes.Contains(raw.AppTheme ?? string.Empty) ? raw.AppTheme : "System",
            BarOpacity = Math.Clamp(raw.BarOpacity, 0.3, 1.0),
            UseFixedTabWidth = raw.UseFixedTabWidth,
            ShowBranding = raw.ShowBranding,
            Language = string.IsNullOrWhiteSpace(raw.Language) ? "en" : raw.Language.Trim(),
            AccentColor = string.IsNullOrWhiteSpace(raw.AccentColor) ? "blue" : raw.AccentColor.Trim().ToLowerInvariant(),
            AutoHideBar = raw.AutoHideBar,
            LaunchAtStartup = raw.LaunchAtStartup,
            ShowClock = raw.ShowClock,
            ClockSize = AllowedClockSizes.Contains(raw.ClockSize ?? string.Empty) ? raw.ClockSize : "Small",
            ShowNotificationBadges = raw.ShowNotificationBadges,
            NotificationDotSize = Math.Clamp(raw.NotificationDotSize, 4, 12),
            NotificationDotColor = NormaliseHex(raw.NotificationDotColor),
            BarSize = AllowedBarSizes.Contains(raw.BarSize ?? string.Empty) ? raw.BarSize : "Small",
            UseBlurEffect = raw.UseBlurEffect,
            BlurMode = AllowedBlurModes.Contains(raw.BlurMode ?? string.Empty) ? raw.BlurMode : "Acrylic",
            AutoCheckUpdates = raw.AutoCheckUpdates,
            ActiveTabColor = NormaliseHex(raw.ActiveTabColor),
            ActiveTabOpacity = Math.Clamp(raw.ActiveTabOpacity, 0, 100)
        };
    }

    /// <summary>
    /// Returns a canonical upper-case <c>#RRGGBB</c> string when the input
    /// matches the strict pattern, or <see cref="string.Empty"/> for any
    /// invalid value. Empty is the sentinel "follow the accent color" used
    /// throughout the UI.
    /// </summary>
    private static string NormaliseHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return HexColorPattern.IsMatch(trimmed) ? trimmed.ToUpperInvariant() : string.Empty;
    }

    private static string DefaultFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tabu");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, SettingsFileName);
    }
}
