using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

using JapaneseDictionary.Services;

namespace JapaneseDictionary;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public const string EntryIdExtra = "com.aitorsansal.japanesedictionary.ENTRY_ID";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // ThemeService applies the theme before this window exists, so set the
        // bar icons for the starting theme here (later changes go through it).
        SystemBars.Apply(Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark);

        // A recreated activity (rotation, process restore) still carries the
        // original intent; only a fresh launch should open the entry.
        if (savedInstanceState == null && TryGetEntryId(Intent) is { } entryId)
            EntryLinks.Open(entryId);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (TryGetEntryId(intent) is { } entryId)
            EntryLinks.Open(entryId);
    }

    private static int? TryGetEntryId(Intent? intent) =>
        intent?.GetIntExtra(EntryIdExtra, -1) is > 0 and var id ? id : null;
}
