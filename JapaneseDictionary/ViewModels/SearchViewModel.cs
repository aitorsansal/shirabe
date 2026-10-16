using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private const int RecentsLimit = 6;
    private const int ResultsLimit = 50;

    private readonly AppDataContext data;
    private CancellationTokenSource? searchCts;

    public SearchViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private string query = "";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string resultLabel = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecents))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool isPreparingDictionary;

    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public ObservableCollection<RecentWordChip> Recents { get; } = [];

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
    public bool HasRecents => Recents.Count > 0;
    public bool ShowHomeContent => !HasQuery;
    public bool ShowRecents => !HasQuery && !IsPreparingDictionary;
    public bool IsEmpty => HasQuery && !IsLoading && !IsPreparingDictionary && Results.Count == 0;
    public string Placeholder => "食べる · taberu · to eat";
    public string ResultCountText => Results.Count == 1 ? "1 entry" : $"{Results.Count} entries";

    public async Task OnAppearingAsync()
    {
        // Only the first launch after an install or a dictionary update copies the
        // ~130MB database, which takes a few seconds; every other start skips this.
        if (!data.IsInitialized && !await DatabaseInitializer.IsDictionaryUpToDateAsync())
            IsPreparingDictionary = true;

        await data.EnsureInitializedAsync();
        IsPreparingDictionary = false;
        RefreshRecents();

        await EntryLinks.OpenPendingAsync();
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(ShowRecents));
        OnPropertyChanged(nameof(ShowHomeContent));
        _ = RunSearchAsync();
    }

    [RelayCommand]
    private void ClearQuery() => Query = "";

    [RelayCommand]
    private void ClearRecents()
    {
        data.UserDataRepository.ClearHistory();
        RefreshRecents();
    }

    [RelayCommand]
    private async Task OpenDrawAsync()
    {
        var page = App.Services.GetRequiredService<HandwritingSheetPage>();
        page.OnCharacterPicked = character => Query += character;
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private static Task OpenSettingsAsync() => Shell.Current.GoToAsync(nameof(SettingsPage));

    [RelayCommand]
    private Task OpenResultAsync(int entryId) => OpenEntryAsync(entryId);

    [RelayCommand]
    private Task OpenRecentAsync(int entryId) => OpenEntryAsync(entryId);

    private async Task OpenEntryAsync(int entryId)
    {
        await data.EnsureInitializedAsync();
        data.UserDataRepository.RecordView(entryId);
        await Shell.Current.GoToAsync($"{nameof(WordDetailPage)}?entryId={entryId}");
    }

    private async Task RunSearchAsync()
    {
        // Callers fire-and-forget this (`_ = RunSearchAsync()`), so an exception here would
        // otherwise vanish and leave the list silently empty (that is how a missing native
        // SQLite library showed up as "0 entries"). Log it instead.
        try { await RunSearchCoreAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Search failed: {ex}"); }
    }

    private async Task RunSearchCoreAsync()
    {
        searchCts?.Cancel();
        var cts = searchCts = new CancellationTokenSource();
        var token = cts.Token;

        try
        {
            if (!HasQuery)
            {
                Results.Clear();
                return;
            }

            // Debounce: a keystroke that's immediately followed by another one
            // cancels this delay and never reaches the actual search — so typing
            // "たべ" fires one search, not three.
            try
            {
                await Task.Delay(200, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            IsLoading = true;
            OnPropertyChanged(nameof(IsEmpty));

            await data.EnsureInitializedAsync();
            if (token.IsCancellationRequested) return;

            var query = Query;
            var (rows, label) = await Task.Run(() => BuildResultRows(query), token);
            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var row in rows) Results.Add(row);
            ResultLabel = label;
        }
        finally
        {
            // Only the newest search owns the loading state: a superseded one
            // returning early must not hide the spinner of the search that
            // replaced it, and clearing the query mid-search must not leave it on.
            if (searchCts == cts)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(ResultCountText));
            }
        }
    }

    private (List<SearchResultRow> Rows, string Label) BuildResultRows(string query)
    {
        var matches = data.SearchService.Search(query, ResultsLimit);
        var hasEnglish = matches.Any(m => m.Kind == SearchMatchKind.Gloss);
        var hasJapanese = matches.Any(m => m.Kind != SearchMatchKind.Gloss);
        var isMixed = hasEnglish && hasJapanese;

        var rows = new List<SearchResultRow>();
        foreach (var match in matches)
        {
            var entry = data.DictionaryRepository.GetEntry(match.EntryId);
            if (entry == null) continue;

            rows.Add(SearchResultRow.FromEntry(entry, MatchNote(match, query, isMixed)));
        }

        var label = isMixed ? "Japanese · English"
            : hasEnglish ? "English → Japanese"
            : "Japanese → English";
        return (rows, label);
    }

    // When one Latin query matches both ways ("go": 碁 as romaji, 行く as "to go"),
    // each row says which reading of the query found it.
    private static string? MatchNote(SearchResult match, string query, bool isMixed) => match.Kind switch
    {
        SearchMatchKind.Conjugated => $"de-inflected from {match.MatchedText ?? query} ({match.ConjugationForm})",
        SearchMatchKind.Gloss when isMixed => $"English: {match.MatchedText}",
        _ when isMixed && match.MatchedText is { } kana && kana != query => $"romaji: {kana}",
        _ => null,
    };

    private void RefreshRecents()
    {
        Recents.Clear();
        foreach (var id in data.UserDataRepository.GetRecentHistory(RecentsLimit))
        {
            var entry = data.DictionaryRepository.GetEntry(id);
            if (entry != null) Recents.Add(new RecentWordChip(id, entry.PrimaryText));
        }

        OnPropertyChanged(nameof(HasRecents));
    }
}
