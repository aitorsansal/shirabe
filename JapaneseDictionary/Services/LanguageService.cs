using System.Globalization;

using JapaneseDictionary.Resources.Localization;

namespace JapaneseDictionary.Services;

/// <summary>
/// Owns the UI language: follow the system (empty preference), or pin one of
/// <see cref="Supported"/>. Strings come from Resources/Localization/AppResources
/// (English neutral, .es and .ca satellites); any other system language falls
/// back to English through normal resource fallback.
///
/// Only the UI is translated: glosses, search and sentence translations come
/// from the English JMdict/Tatoeba data whatever the language.
///
/// XAML reads strings through x:Static when a page is built, so a change only
/// shows on pages built afterwards: the caller rebuilds the Shell.
/// </summary>
public sealed class LanguageService
{
    private const string PreferenceKey = "ui_language";

    public static readonly IReadOnlyList<string> Supported = ["en", "es", "ca"];

    /// <summary>The device's language as it was before any override, so
    /// switching back to "system" at runtime restores it.</summary>
    private static CultureInfo? systemUiCulture;

    /// <summary>"" (follow the system) or one of <see cref="Supported"/>.</summary>
    public static string Preference
    {
        get
        {
            var stored = Preferences.Default.Get(PreferenceKey, "");
            return Supported.Contains(stored) ? stored : "";
        }
    }

    /// <summary>Called first thing in MauiProgram, before any page or string is
    /// created (and so also before the Look Up popup, which shares the process).</summary>
    public static void ApplyAtStartup()
    {
        systemUiCulture ??= CultureInfo.CurrentUICulture;
        Apply(Preference);
    }

    public void SetPreference(string language)
    {
        if (language.Length > 0 && !Supported.Contains(language)) return;
        Preferences.Default.Set(PreferenceKey, language);
        Apply(language);
    }

    /// <summary>The culture every UI string is looked up in.</summary>
    public static CultureInfo Culture => AppResources.Culture ?? CultureInfo.CurrentUICulture;

    private static void Apply(string language)
    {
        var culture = language.Length == 0 ? systemUiCulture ?? CultureInfo.CurrentUICulture : new CultureInfo(language);

        // AppResources.Culture is a plain static that every generated getter reads,
        // so it's the one that actually decides the language. CurrentUICulture alone
        // isn't enough: it flows with the async context, so setting it inside an
        // async command (the language picker) is undone when that command returns,
        // and pages built afterwards would come out in the old language.
        AppResources.Culture = culture;

        // Still set for anything else that reads the UI culture. UI culture only:
        // number and date formatting keep following the device.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
