using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

/// <summary>
/// Words containing one kanji, reached by tapping its tile on the word page.
/// It is the ordinary search for that character, so it shows what typing the
/// kanji would; it is a pushed page of its own (not the root search page, with
/// its recents and hints), so Back returns to the word page it came from.
/// </summary>
[QueryProperty(nameof(Kanji), "kanji")]
public sealed partial class KanjiWordsViewModel : ObservableObject
{
    private const int ResultsLimit = 50;

    private readonly AppDataContext data;

    public KanjiWordsViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private string kanji = "";

    [ObservableProperty]
    private string countText = "";

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private bool isEmpty;

    public ObservableCollection<SearchResultRow> Rows { get; } = [];

    // Loaded once: nothing on this page changes while a word page is open above it.
    public async Task OnAppearingAsync()
    {
        if (Rows.Count > 0 || string.IsNullOrEmpty(Kanji)) return;
        await data.EnsureInitializedAsync();

        var query = Kanji;
        var rows = await Task.Run(() => data.SearchService.Search(query, ResultsLimit)
            .Select(match => data.DictionaryRepository.GetEntry(match.EntryId))
            .OfType<Core.Models.DictionaryEntry>()
            .Select(entry => SearchResultRow.FromEntry(entry))
            .ToList());

        foreach (var row in rows) Rows.Add(row);
        CountText = UiText.EntryCount(Rows.Count);
        IsEmpty = Rows.Count == 0;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task OpenEntryAsync(int entryId)
    {
        data.UserDataRepository.RecordView(entryId);
        await Shell.Current.GoToAsync($"{nameof(WordDetailPage)}?entryId={entryId}");
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
