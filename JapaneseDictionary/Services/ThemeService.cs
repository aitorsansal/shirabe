using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JapaneseDictionary.Services;

/// <summary>
/// Owns the app's light/dark theme choice. A manual toggle (not just "follow
/// system") is exposed via <see cref="ToggleThemeCommand"/>, persisted so it
/// survives restarts; <see cref="IsDark"/> is kept in sync with the
/// effective theme (including system changes while no manual override is
/// active) so the hue-cycling chip converters — which can't react to
/// AppThemeBinding themselves, see HueConverters.cs — can re-run on toggle.
/// </summary>
public sealed partial class ThemeService : ObservableObject
{
    private const string PreferenceKey = "theme_preference";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon))]
    private bool isDark;

    public string ThemeIcon => IsDark ? "☀" : "☾";

    public ThemeService()
    {
        var stored = Preferences.Default.Get(PreferenceKey, "System");
        Application.Current!.UserAppTheme = stored switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        IsDark = ResolveIsDark();
        Application.Current.RequestedThemeChanged += (_, _) => IsDark = ResolveIsDark();
    }

    private static bool ResolveIsDark() => Application.Current!.RequestedTheme == AppTheme.Dark;

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = IsDark ? AppTheme.Light : AppTheme.Dark;
        Application.Current!.UserAppTheme = next;
        Preferences.Default.Set(PreferenceKey, next.ToString());
        IsDark = ResolveIsDark();
    }
}
