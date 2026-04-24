using System.Windows;

namespace Tabu.UI.Services;

/// <summary>
/// Manages runtime language switching by swapping a locale ResourceDictionary.
/// </summary>
public sealed class LocalizationManager
{
    private const string LocalePrefix = "Locales/";
    private const string LocaleSuffix = ".xaml";
    private ResourceDictionary? _currentLocale;

    public static readonly IReadOnlyList<LanguageOption> AvailableLanguages = new List<LanguageOption>
    {
        new("en", "English"),
        new("es", "Español"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("pt", "Português"),
        new("it", "Italiano"),
        new("ja", "日本語"),
        new("zh", "中文"),
        new("ko", "한국어"),
        new("ru", "Русский")
    };

    public void Apply(string languageCode)
    {
        // Whitelist guard: a tampered settings.json could otherwise inject
        // a relative URI that resolves to an unintended embedded resource.
        // Falling back to English keeps the UI usable on any unknown code.
        var resolved = AvailableLanguages.Any(l =>
            string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase))
            ? languageCode
            : "en";

        var mergedDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;

        if (_currentLocale is not null)
        {
            mergedDictionaries.Remove(_currentLocale);
        }

        var source = $"{LocalePrefix}{resolved}{LocaleSuffix}";
        var newLocale = new ResourceDictionary
        {
            Source = new System.Uri(source, System.UriKind.Relative)
        };

        mergedDictionaries.Add(newLocale);
        _currentLocale = newLocale;
    }
}

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
