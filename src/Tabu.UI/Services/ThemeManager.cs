using Microsoft.Win32;

namespace Tabu.UI.Services;

/// <summary>
/// Manages application theme switching between Dark, Light, and System modes.
/// Swaps the Colors resource dictionary at runtime.
/// </summary>
public enum AppTheme
{
    System,
    Dark,
    Light
}

public sealed class ThemeManager
{
    private const string ColorsKeyDark = "Styles/Colors.xaml";
    private const string ColorsKeyLight = "Styles/ColorsLight.xaml";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private AppTheme _currentSetting = AppTheme.System;

    public AppTheme CurrentSetting => _currentSetting;

    public void Apply(AppTheme theme)
    {
        _currentSetting = theme;

        bool useDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDarkMode()
        };

        string targetUri = useDark ? ColorsKeyDark : ColorsKeyLight;
        SwapColorsDictionary(targetUri);
    }

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            // 0 = dark mode, 1 = light mode
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return true; // Default to dark if registry read fails
        }
    }

    private static void SwapColorsDictionary(string newSourcePath)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;

        var mergedDicts = app.Resources.MergedDictionaries;

        // Find and remove existing Colors dictionary
        System.Windows.ResourceDictionary? existing = null;
        foreach (var dict in mergedDicts)
        {
            if (dict.Source is not null &&
                (dict.Source.OriginalString.Contains("Colors.xaml") ||
                 dict.Source.OriginalString.Contains("ColorsLight.xaml")))
            {
                existing = dict;
                break;
            }
        }

        if (existing is not null)
        {
            mergedDicts.Remove(existing);
        }

        // Insert new Colors dictionary — DynamicResource references update automatically
        var newDict = new System.Windows.ResourceDictionary
        {
            Source = new Uri(newSourcePath, UriKind.Relative)
        };
        mergedDicts.Insert(0, newDict);
    }
}
