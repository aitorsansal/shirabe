using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Export;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

/// <summary>
/// Contents of one list. <see cref="HistoryListId"/> shows the view history
/// through the same screen, read-only. Entries whose id no longer exists in
/// the bundled dictionary (JMdict drops or merges entries between releases)
/// are skipped rather than shown blank.
/// </summary>
[QueryProperty(nameof(ListId), "listId")]
public sealed partial class ListDetailViewModel : ObservableObject
{
    /// <summary>Not a real list id: WordListRow ids start at 1 (Favourites).</summary>
    public const int HistoryListId = 0;

    private const int HistoryLimit = 200;

    private readonly AppDataContext data;
    private List<DictionaryEntry> entries = [];
    private IReadOnlyDictionary<int, int> jlptLevels = new Dictionary<int, int>();

    public ListDetailViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private int listId = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kicker))]
    private string title = "";

    [ObservableProperty]
    private string countText = "";

    [ObservableProperty]
    private bool isLoading = true;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private bool isUserList;

    public bool IsHistory => ListId == HistoryListId;
    public bool CanRemove => !IsHistory;
    public bool CanExport => !IsEmpty;
    public bool CanSendToPractice => !IsEmpty && !IsHistory;
    public string Kicker => IsHistory ? AppResources.Lists_KickerHistory : IsUserList ? AppResources.Lists_KickerList : AppResources.Lists_KickerSaved;

    public string EmptyText => IsHistory ? AppResources.Lists_EmptyHistory : AppResources.Lists_EmptyList;

    public ObservableCollection<SearchResultRow> Rows { get; } = [];

    partial void OnListIdChanged(int value)
    {
        OnPropertyChanged(nameof(IsHistory));
        OnPropertyChanged(nameof(CanSendToPractice));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(Kicker));
    }

    partial void OnIsEmptyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanSendToPractice));
    }

    /// <summary>Reloaded on every appearance: coming back from a word page may
    /// have changed membership (un-favourited, removed from this list).</summary>
    public async Task OnAppearingAsync()
    {
        if (ListId < 0) return;
        await data.EnsureInitializedAsync();

        var repository = data.UserDataRepository;
        if (IsHistory)
        {
            Title = AppResources.Common_History;
            IsUserList = false;
        }
        else if (repository.GetList(ListId) is { } list)
        {
            Title = UiText.ListName(list);
            IsUserList = !list.IsSystem;
        }
        else
        {
            // Deleted while this page was in the back stack.
            await Shell.Current.GoToAsync("..");
            return;
        }

        var listId = ListId;
        var loaded = await Task.Run(() =>
        {
            var ids = listId == HistoryListId ? repository.GetRecentHistory(HistoryLimit) : repository.GetListEntries(listId);
            var found = ids.Select(data.DictionaryRepository.GetEntry).OfType<DictionaryEntry>().ToList();
            return (Entries: found, Levels: data.DictionaryRepository.GetJlptLevels(found.Select(e => e.Id).ToList()));
        });

        entries = loaded.Entries;
        jlptLevels = loaded.Levels;
        Rows.Clear();
        foreach (var entry in loaded.Entries)
            Rows.Add(SearchResultRow.FromEntry(entry, loaded.Levels.TryGetValue(entry.Id, out var level) ? level : null));
        UpdateCounts();
        IsLoading = false;
    }

    [RelayCommand]
    private async Task OpenEntryAsync(int entryId)
    {
        data.UserDataRepository.RecordView(entryId);
        await Shell.Current.GoToAsync($"{nameof(WordDetailPage)}?entryId={entryId}");
    }

    [RelayCommand]
    private void Remove(SearchResultRow row)
    {
        if (!CanRemove) return;
        data.UserDataRepository.RemoveFromList(ListId, row.EntryId);
        Rows.Remove(row);
        entries.RemoveAll(e => e.Id == row.EntryId);
        UpdateCounts();
    }

    [RelayCommand]
    private async Task ShowListMenuAsync()
    {
        var rename = AppResources.Lists_Rename;
        var delete = AppResources.Lists_Delete;
        var choice = await Shell.Current.DisplayActionSheetAsync(Title, AppResources.Common_Cancel, delete, rename);

        if (choice == rename)
        {
            var name = await ListNamePrompt.AskAsync(AppResources.Lists_RenameTitle, "", AppResources.Lists_Rename, Title, ListId);
            if (name == null) return;
            data.UserDataRepository.RenameList(ListId, name);
            Title = name;
        }
        else if (choice == delete)
        {
            var confirmed = await Shell.Current.DisplayAlertAsync(
                UiText.Format(AppResources.Lists_DeleteTitle, Title), AppResources.Lists_DeleteMessage, AppResources.Lists_DeleteConfirm, AppResources.Common_Cancel);
            if (!confirmed) return;
            data.UserDataRepository.DeleteList(ListId);
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task SendToPracticeAsync()
    {
        if (!CanSendToPractice) return;
        var status = PracticeApp.GetStatus();
        if (status != PracticeAppStatus.Ok)
        {
            await ShowPracticePromptAsync(status);
            return;
        }

        var snapshot = entries.ToList();
        var levels = jlptLevels;
        var listName = Title;
        var languages = TranslationPreferences.Selected;
        var repository = data.DictionaryRepository;
        var requestId = Guid.NewGuid();
        var result = await Task.Run(() => CompanionExport.Build(
            snapshot.Select(e => new CompanionExport.Row(
                e,
                levels.TryGetValue(e.Id, out var level) ? level : null,
                repository.GetTranslatedSenses(e.Id, languages))),
            listName, requestId));

        if (result.Exported == 0)
        {
            await Shell.Current.DisplayAlertAsync(AppResources.Lists_SendToPractice, AppResources.Lists_SendNothing, AppResources.Common_Done);
            return;
        }
        if (result.Json.Length > 2 * 1024 * 1024)
        {
            await Shell.Current.DisplayAlertAsync(AppResources.Lists_SendToPractice, AppResources.Lists_SendTooLarge, AppResources.Common_Done);
            return;
        }
        if (result.Truncated)
            await Shell.Current.DisplayAlertAsync(AppResources.Lists_SendToPractice,
                UiText.Format(AppResources.Lists_SendTruncated, CompanionExport.MaxEntries), AppResources.Common_Done);

        if (result.Skipped > 0)
            await Shell.Current.DisplayAlertAsync(AppResources.Lists_SendToPractice,
                UiText.Format(AppResources.Lists_SendSkipped, result.Skipped), AppResources.Common_Done);

        status = await PracticeApp.SendAsync(result.Json, requestId);
        if (status != PracticeAppStatus.Ok) await ShowPracticePromptAsync(status);
    }

    private static async Task ShowPracticePromptAsync(PracticeAppStatus status)
    {
        var absent = status == PracticeAppStatus.Absent;
        var accepted = await Shell.Current.DisplayAlertAsync(
            absent ? AppResources.Lists_SendAbsentTitle : AppResources.Lists_SendUpdateTitle,
            absent ? AppResources.Lists_SendAbsentBody : AppResources.Lists_SendUpdateBody,
            absent ? AppResources.Lists_SendGetIt : AppResources.Lists_SendUpdateAction,
            AppResources.Common_Cancel);
        if (accepted) await PracticeApp.OpenStoreAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (entries.Count == 0) return;

        var snapshot = entries.ToList();
        var levels = jlptLevels;
        var listName = Title;
        var languages = TranslationPreferences.Selected;
        var repository = data.DictionaryRepository;

        var csv = await Task.Run(() => WordListCsv.Build(
            snapshot.Select(e => new WordListCsv.Row(
                e,
                levels.TryGetValue(e.Id, out var level) ? level : null,
                repository.GetTranslatedSenses(e.Id, languages))).ToList(),
            listName));

        var path = Path.Combine(FileSystem.CacheDirectory, $"{SafeFileName(Title)}.csv");
        await File.WriteAllBytesAsync(path, WordListCsv.ToBytes(csv));

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = UiText.Format(AppResources.Lists_ExportTitle, Title),
            File = new ShareFile(path, "text/csv"),
        });
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private void UpdateCounts()
    {
        CountText = UiText.WordCount(Rows.Count);
        IsEmpty = Rows.Count == 0;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length > 0 ? cleaned : "list";
    }
}
