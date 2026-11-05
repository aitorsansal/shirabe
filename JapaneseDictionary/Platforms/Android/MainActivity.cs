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
        // original intent, and so does a relaunch from Recents; only a fresh
        // launch should open the entry.
        if (savedInstanceState == null && TryGetEntryId(Intent) is { } entryId)
            EntryLinks.Open(entryId);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (TryGetEntryId(intent) is { } entryId)
            EntryLinks.Open(entryId);
    }

    // A request for a newer companion contract than this build knows is dropped
    // rather than half-understood (docs/COMPANION_CONTRACT.md, Versioning); the
    // launcher has no popup to explain it in, so the app just opens normally.
    private static int? TryGetEntryId(Intent? intent) =>
        CompanionIntents.IsReplayFromRecents(intent) || CompanionIntents.NeedsNewerApp(intent) ? null
        : intent?.GetIntExtra(EntryIdExtra, -1) is > 0 and var id ? id : null;
}
