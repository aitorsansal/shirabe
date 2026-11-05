using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

using Microsoft.Extensions.DependencyInjection;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary;

// System "Look Up" entry: appears in every app's text-selection toolbar
// (Copy/Share/…) once this intent filter is declared. See
// docs/process-text-lookup-plan.md for the design rationale — this is a
// deliberately separate, non-Shell-routed Activity (not MainActivity) so a
// lookup shows as a small floating card over whatever app the user was in,
// rather than switching to the full app.
[Activity(Label = "@string/lookup_label", Theme = "@style/LookupDialogTheme", Exported = true)]
[IntentFilter(
    [Android.Content.Intent.ActionProcessText],
    Categories = [Android.Content.Intent.CategoryDefault],
    DataMimeType = "text/plain")]
public class ProcessTextLookupActivity : Android.App.Activity
{
    private TextView wordText = null!;
    private TextView readingText = null!;
    private TextView glossText = null!;
    private TextView openButton = null!;
    private Android.Widget.ProgressBar progress = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.process_text_lookup);

        wordText = FindViewById<TextView>(Resource.Id.lookupWord)!;
        readingText = FindViewById<TextView>(Resource.Id.lookupReading)!;
        glossText = FindViewById<TextView>(Resource.Id.lookupGlosses)!;
        openButton = FindViewById<TextView>(Resource.Id.lookupOpen)!;
        progress = FindViewById<Android.Widget.ProgressBar>(Resource.Id.lookupProgress)!;
        openButton.Text = AppResources.Lookup_Open;

        if (CompanionIntents.NeedsNewerApp(Intent))
        {
            ShowMessage(AppResources.Companion_UpdateShirabe);
            return;
        }

        ShowLoading();

        var selectedText = Intent?.GetStringExtra(Android.Content.Intent.ExtraProcessText);
        _ = LookUpAsync(selectedText, CompanionIntents.ReadReading(Intent));
    }

    private void ShowLoading()
    {
        progress.Visibility = ViewStates.Visible;
        wordText.Visibility = ViewStates.Gone;
        readingText.Visibility = ViewStates.Gone;
        glossText.Visibility = ViewStates.Gone;
    }

    private async Task LookUpAsync(string? selectedText, string? reading)
    {
        var query = selectedText?.Trim();

        try
        {
            if (string.IsNullOrEmpty(query))
            {
                ShowMessage(AppResources.Lookup_NothingSelected);
                return;
            }

            // First consumer that might be the first thing in the process to
            // touch AppDataContext — measured ~40ms even cold in the Gate 1
            // spike, so no separate "still initializing" sub-state is shown;
            // it's covered by the same loading spinner as the search itself.
            var services = IPlatformApplication.Current?.Services
                ?? throw new InvalidOperationException("MAUI service provider was not available.");
            var appDataContext = services.GetRequiredService<AppDataContext>();
            await appDataContext.EnsureInitializedAsync();

            var entry = FindBestEntry(appDataContext.DictionaryRepository, appDataContext.SearchService, query, reading);
            if (entry is null)
            {
                ShowMessage(UiText.Format(AppResources.Lookup_NotFound, query));
                return;
            }

            ShowEntry(entry, PopupGlosses(appDataContext.DictionaryRepository, entry));
        }
        catch (Exception)
        {
            // Deliberately no dedicated error UI here — see
            // docs/process-text-lookup-plan.md §7, open question #4.
            ShowMessage(AppResources.Lookup_Failed);
        }
    }

    private static DictionaryEntry? FindBestEntry(DictionaryRepository repo, SearchService search, string query, string? reading) =>
        LookupSelector.PickEntry(search, query, reading) is { } id ? repo.GetEntry(id) : null;

    // The popup has room for one language. It uses the UI language's meanings when
    // that language is also on in Settings > translations (Catalan UI counts as
    // Spanish, as for the translation defaults) and the word has some; otherwise
    // English, which every entry has.
    private static IEnumerable<string> PopupGlosses(DictionaryRepository repo, DictionaryEntry entry)
    {
        var iso = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (iso == "ca") iso = "es";
        var language = TranslationLanguages.All.FirstOrDefault(l => l.Iso1 == iso);
        if (language == null || !TranslationPreferences.IsSelected(language.Code)) return entry.MeaningGlosses;

        var translated = repo.GetTranslatedSenses(entry.Id, [language.Code])
            .SelectMany(s => s.Glosses)
            .Where(g => g.Length > 0)
            .Distinct()
            .ToList();
        return translated.Count > 0 ? translated : entry.MeaningGlosses;
    }

    private void ShowEntry(DictionaryEntry entry, IEnumerable<string> meanings)
    {
        var glosses = string.Join(" / ", meanings.Take(3));

        progress.Visibility = ViewStates.Gone;
        wordText.Visibility = ViewStates.Visible;
        readingText.Visibility = ViewStates.Visible;
        glossText.Visibility = ViewStates.Visible;

        wordText.Text = entry.PrimaryText;
        readingText.Text = entry.PrimaryReading + (JapaneseDictionary.Services.ReadingPreferences.Instance.ShowRomaji
            ? "\n" + JapaneseDictionary.Core.Search.RomajiConverter.ToRomaji(entry.PrimaryReading) : "");
        glossText.Text = glosses.Length > 0 ? glosses : AppResources.Lookup_NoGlosses;

        openButton.Visibility = ViewStates.Visible;
        openButton.Click += (_, _) => OpenInApp(entry.Id);
    }

    // NewTask brings Shirabe's own task forward (this popup lives in the calling
    // app's task); ClearTop + SingleTop reuse a running MainActivity through
    // OnNewIntent instead of stacking a second copy of the app.
    private void OpenInApp(int entryId)
    {
        var intent = new Android.Content.Intent(this, typeof(MainActivity));
        intent.PutExtra(MainActivity.EntryIdExtra, entryId);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.ClearTop | Android.Content.ActivityFlags.SingleTop);
        StartActivity(intent);
        Finish();
    }

    private void ShowMessage(string message)
    {
        progress.Visibility = ViewStates.Gone;
        wordText.Visibility = ViewStates.Visible;
        readingText.Visibility = ViewStates.Gone;
        glossText.Visibility = ViewStates.Visible;

        wordText.Text = AppResources.Lookup_Title;
        glossText.Text = message;
    }
}
