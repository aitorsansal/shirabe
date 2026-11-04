using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private const int RecentsLimit = 6;
    private const int ResultsLimit = 50;
    private const int ExpandedResultsLimit = 200;

    private readonly AppDataContext data;
    private int resultsFetchedWith = ResultsLimit;
    private CancellationTokenSource? searchCts;

    public SearchViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private string query = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowMore))]
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
    // True once the first page hit its cap, so there may be more to fetch.
    public bool CanShowMore => !IsLoading && resultsFetchedWith == ResultsLimit && Results.Count == ResultsLimit;
    public string Placeholder => "食べる · taberu · to eat";
    public string ResultCountText => UiText.EntryCount(Results.Count);

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
        page.Attach(() => Query, text => Query = text);
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private static Task OpenSettingsAsync() => Shell.Current.GoToAsync(nameof(SettingsPage));

    [RelayCommand]
    private static Task OpenListsAsync() => Shell.Current.GoToAsync(nameof(ListsPage));

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
                resultsFetchedWith = ResultsLimit;
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
            var (rows, label) = await Task.Run(() => BuildResultRows(query, ResultsLimit), token);
            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var row in rows) Results.Add(row);
            ResultLabel = label;
            resultsFetchedWith = ResultsLimit;
        }
        finally
        {
            // Only the newest search owns the loading state: a superseded one
            // returning early must not hide the spinner of the search that
            // replaced it, and clearing the query mid-search must not leave it on.
            if (searchCts == cts) FinishSearch();
        }
    }

    private void FinishSearch()
    {
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(CanShowMore));
    }

    [RelayCommand]
    private async Task ShowMoreAsync()
    {
        try { await ShowMoreCoreAsync(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Show more failed: {ex}"); }
    }

    private async Task ShowMoreCoreAsync()
    {
        if (!CanShowMore) return;

        // Shares searchCts with normal searches, so typing a new query drops this one.
        searchCts?.Cancel();
        var cts = searchCts = new CancellationTokenSource();
        var token = cts.Token;

        try
        {
            IsLoading = true;
            OnPropertyChanged(nameof(IsEmpty));

            var query = Query;
            var (rows, _) = await Task.Run(() => BuildResultRows(query, ExpandedResultsLimit), token);
            if (token.IsCancellationRequested) return;

            // Append only the new rows so the ones already shown keep their place.
            var known = Results.Select(r => r.EntryId).ToHashSet();
            foreach (var row in rows)
                if (known.Add(row.EntryId)) Results.Add(row);
            resultsFetchedWith = ExpandedResultsLimit;
        }
        finally
        {
            if (searchCts == cts) FinishSearch();
        }
    }

    private (List<SearchResultRow> Rows, string Label) BuildResultRows(string query, int limit)
    {
        var matches = data.SearchService.Search(query, limit);
        var hasEnglish = matches.Any(m => m.Kind == SearchMatchKind.Gloss);
        var hasJapanese = matches.Any(m => m.Kind != SearchMatchKind.Gloss);
        var isMixed = hasEnglish && hasJapanese;

        var levels = data.DictionaryRepository.GetJlptLevels(matches.Select(m => m.EntryId).Distinct().ToList());
        var rows = new List<SearchResultRow>();
        foreach (var match in matches)
        {
            var entry = data.DictionaryRepository.GetEntry(match.EntryId);
            if (entry == null) continue;

            rows.Add(SearchResultRow.FromEntry(
                entry,
                levels.TryGetValue(entry.Id, out var level) ? level : null,
                MatchNote(match, query, isMixed),
                match.Kind == SearchMatchKind.Gloss ? match.MatchedText : null));
        }

        var label = isMixed ? AppResources.Search_LabelMixed
            : hasEnglish ? AppResources.Search_LabelEnglish
            : AppResources.Search_LabelJapanese;
        return (rows, label);
    }

    // When one Latin query matches both ways ("go": 碁 as romaji, 行く as "to go"),
    // each row says which reading of the query found it.
    private static string? MatchNote(SearchResult match, string query, bool isMixed) => match.Kind switch
    {
        SearchMatchKind.Conjugated => UiText.Format(AppResources.Search_NoteConjugated, match.MatchedText ?? query, UiText.FormName(match.ConjugationForm)),
        SearchMatchKind.Gloss when isMixed => UiText.Format(AppResources.Search_NoteEnglish, match.MatchedText ?? ""),
        _ when isMixed && match.MatchedText is { } kana && kana != query => UiText.Format(AppResources.Search_NoteRomaji, kana),
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
