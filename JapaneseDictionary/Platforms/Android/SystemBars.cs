using AndroidX.Core.View;

namespace JapaneseDictionary;

/// <summary>
/// Keeps the status and navigation bar icons readable against the app's own
/// background. From Android 15 the app draws edge-to-edge behind transparent
/// bars, so the icons must follow the effective theme: dark icons on the light
/// theme, light icons on the dark one (without this they stayed dark on the dark
/// theme). Below Android 15 the bars keep the theme's coloured background, whose
/// light icons fit both themes, so nothing is changed there.
/// </summary>
public static class SystemBars
{
    public static void Apply(bool isDark)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(35)) return;
        if (Platform.CurrentActivity?.Window is not { } window) return;

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        controller.AppearanceLightStatusBars = !isDark;
        controller.AppearanceLightNavigationBars = !isDark;
    }
}
