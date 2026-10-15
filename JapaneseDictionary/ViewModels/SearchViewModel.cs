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
    private bool isJapaneseMode = true;

    [ObservableProperty]
    private bool isLoading;

    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public ObservableCollection<RecentWordChip> Recents { get; } = [];

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
    public bool ShowRecents => !HasQuery;
    public bool IsEmpty => HasQuery && !IsLoading && Results.Count == 0;
    public string Placeholder => IsJapaneseMode ? "食べる · たべる · taberu" : "to eat, book, school…";
    public string ResultLabel => IsJapaneseMode ? "Japanese → English" : "English → Japanese";
    public string ResultCountText => Results.Count == 1 ? "1 entry" : $"{Results.Count} entries";

    public async Task OnAppearingAsync()
    {
        await data.EnsureInitializedAsync();
        RefreshRecents();
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasQuery));
        OnPropertyChanged(nameof(ShowRecents));
        _ = RunSearchAsync();
    }

    [RelayCommand]
    private void ClearQuery() => Query = "";

    [RelayCommand]
    private void SetJapaneseMode()
    {
        IsJapaneseMode = true;
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(ResultLabel));
        _ = RunSearchAsync();
    }

    [RelayCommand]
    private void SetEnglishMode()
    {
        IsJapaneseMode = false;
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(ResultLabel));
        _ = RunSearchAsync();
    }

    [RelayCommand]
    private async Task OpenDrawAsync()
    {
        var page = App.Services.GetRequiredService<HandwritingSheetPage>();
        page.OnCharacterPicked = character => Query += character;
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    private static Task OpenAboutAsync() => Shell.Current.GoToAsync(nameof(AboutPage));

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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Search failed: {ex}"); }
    }

    private async Task RunSearchCoreAsync()
    {
        searchCts?.Cancel();
        var cts = searchCts = new CancellationTokenSource();
        var token = cts.Token;

        if (!HasQuery)
        {
            Results.Clear();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ResultCountText));
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

        var mode = IsJapaneseMode ? SearchMode.Japanese : SearchMode.English;
        var query = Query;
        var matches = await Task.Run(() => data.SearchService.Search(query, mode, ResultsLimit), token);
        if (token.IsCancellationRequested) return;

        Results.Clear();
        foreach (var match in matches)
        {
            var entry = data.DictionaryRepository.GetEntry(match.EntryId);
            if (entry == null) continue;

            var firstSense = entry.Senses.FirstOrDefault();
            var tags = firstSense?.PartOfSpeechTags.Take(2)
                .Select((tag, i) => new TagChip(tag, i))
                .ToList() ?? [];
            var gloss = string.Join(" · ", entry.Senses.SelectMany(s => s.Glosses).Take(3));
            var viaNote = match.Kind == SearchMatchKind.Conjugated
                ? $"de-inflected from {Query} ({match.ConjugationForm})"
                : null;

            Results.Add(new SearchResultRow(
                entry.Id, entry.PrimaryReading, entry.PrimaryText,
                entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon),
                tags, gloss, viaNote));
        }

        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultCountText));
    }

    private void RefreshRecents()
    {
        Recents.Clear();
        foreach (var id in data.UserDataRepository.GetRecentHistory(RecentsLimit))
        {
            var entry = data.DictionaryRepository.GetEntry(id);
            if (entry != null) Recents.Add(new RecentWordChip(id, entry.PrimaryText));
        }
    }
}
