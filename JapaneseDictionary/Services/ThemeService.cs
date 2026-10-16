using CommunityToolkit.Mvvm.ComponentModel;

namespace JapaneseDictionary.Services;

public enum ThemePreference { System, Light, Dark }

/// <summary>
/// Owns the app's theme choice: follow the system, or pin light or dark,
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
    private ThemePreference preference;

    public ThemeService()
    {
        Apply(Enum.TryParse<ThemePreference>(Preferences.Default.Get(PreferenceKey, nameof(ThemePreference.System)), out var stored)
            ? stored
            : ThemePreference.System);

        Application.Current!.RequestedThemeChanged += (_, _) => IsDark = ResolveIsDark();
    }

    private static bool ResolveIsDark() => Application.Current!.RequestedTheme == AppTheme.Dark;

    public void SetPreference(ThemePreference next)
    {
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
