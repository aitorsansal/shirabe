using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JapaneseDictionary.Services;

public enum ThemePreference { System, Light, Dark }

/// <summary>
/// Owns the app's theme choice: follow the system, or pin light or dark.
/// <see cref="ToggleThemeCommand"/> cycles System -> Light -> Dark -> System,
/// persisted so it survives restarts. <see cref="IsDark"/> is kept in sync with
/// the effective theme (including system changes while following the system)
/// so the hue-cycling chip converters — which can't react to AppThemeBinding
/// themselves, see HueConverters.cs — can re-run on change.
/// </summary>
public sealed partial class ThemeService : ObservableObject
{
    private const string PreferenceKey = "theme_preference";

    [ObservableProperty]
    private bool isDark;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon))]
    [NotifyPropertyChangedFor(nameof(ThemeDescription))]
    private ThemePreference preference;

    public string ThemeIcon => Preference switch
    {
        // U+FE0E asks for the text glyph; without it ☀ draws as a colour emoji,
        // unlike ☾ and ◐.
        ThemePreference.Light => "☀\uFE0E",
        ThemePreference.Dark => "☾",
        _ => "◐",
    };

    public string ThemeDescription => Preference switch
    {
        ThemePreference.Light => "Theme: light. Tap for dark.",
        ThemePreference.Dark => "Theme: dark. Tap to follow the system.",
        _ => "Theme: following the system. Tap for light.",
    };

    public ThemeService()
    {
        Apply(Enum.TryParse<ThemePreference>(Preferences.Default.Get(PreferenceKey, nameof(ThemePreference.System)), out var stored)
            ? stored
            : ThemePreference.System);

        Application.Current!.RequestedThemeChanged += (_, _) => IsDark = ResolveIsDark();
    }

    private static bool ResolveIsDark() => Application.Current!.RequestedTheme == AppTheme.Dark;

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = Preference switch
        {
            ThemePreference.System => ThemePreference.Light,
            ThemePreference.Light => ThemePreference.Dark,
            _ => ThemePreference.System,
        };

        Preferences.Default.Set(PreferenceKey, next.ToString());
        Apply(next);
    }

    private void Apply(ThemePreference preference)
    {
        Preference = preference;
        Application.Current!.UserAppTheme = preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
        IsDark = ResolveIsDark();
    }
}
