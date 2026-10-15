using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

using Microsoft.Extensions.DependencyInjection;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Services;

namespace JapaneseDictionary;

// System "Look Up" entry: appears in every app's text-selection toolbar
// (Copy/Share/…) once this intent filter is declared. See
// docs/process-text-lookup-plan.md for the design rationale — this is a
// deliberately separate, non-Shell-routed Activity (not MainActivity) so a
// lookup shows as a small floating card over whatever app the user was in,
// rather than switching to the full app.
[Activity(Label = "Look Up", Theme = "@style/LookupDialogTheme", Exported = true)]
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

        ShowLoading();

        var selectedText = Intent?.GetStringExtra(Android.Content.Intent.ExtraProcessText);
        _ = LookUpAsync(selectedText);
    }

    private void ShowLoading()
    {
        progress.Visibility = ViewStates.Visible;
        wordText.Visibility = ViewStates.Gone;
        readingText.Visibility = ViewStates.Gone;
        glossText.Visibility = ViewStates.Gone;
    }

    private async Task LookUpAsync(string? selectedText)
    {
        var query = selectedText?.Trim();

        try
        {
            if (string.IsNullOrEmpty(query))
            {
                ShowMessage("Nothing selected.");
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

            var entry = FindBestEntry(appDataContext.DictionaryRepository, appDataContext.SearchService, query);
            if (entry is null)
            {
                ShowMessage($"No entry found for “{query}”.");
                return;
            }

            ShowEntry(entry);
        }
        catch (Exception)
        {
            // Deliberately no dedicated error UI here — see
            // docs/process-text-lookup-plan.md §7, open question #4.
            ShowMessage("Couldn't look this up right now.");
        }
    }

    // SearchService itself tells Japanese from English (and romaji), so the
    // best match is simply its first result.
    private static DictionaryEntry? FindBestEntry(DictionaryRepository repo, SearchService search, string query) =>
        search.Search(query, limit: 1).FirstOrDefault() is { } best ? repo.GetEntry(best.EntryId) : null;

    private void ShowEntry(DictionaryEntry entry)
    {
        var glosses = string.Join(" / ", entry.Senses.SelectMany(s => s.Glosses).Take(3));

        progress.Visibility = ViewStates.Gone;
        wordText.Visibility = ViewStates.Visible;
        readingText.Visibility = ViewStates.Visible;
        glossText.Visibility = ViewStates.Visible;

        wordText.Text = entry.PrimaryText;
        readingText.Text = entry.PrimaryReading;
        glossText.Text = glosses.Length > 0 ? glosses : "(no glosses)";

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

        wordText.Text = "Look Up";
        glossText.Text = message;
    }
}
