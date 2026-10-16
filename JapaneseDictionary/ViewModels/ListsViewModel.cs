using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

/// <summary>One row on the lists screen: Favourites, History or a user list.</summary>
public sealed record ListSummaryRow(int ListId, string Name, string CountText, string Icon);

public sealed partial class ListsViewModel : ObservableObject
{
    private readonly AppDataContext data;

    public ListsViewModel(AppDataContext data)
    {
        this.data = data;
    }

    /// <summary>Favourites and History: always present, can't be renamed or deleted.</summary>
    public ObservableCollection<ListSummaryRow> BuiltInLists { get; } = [];

    public ObservableCollection<ListSummaryRow> UserLists { get; } = [];

    public bool ShowUserListsHint => UserLists.Count == 0;

    public async Task OnAppearingAsync()
    {
        await data.EnsureInitializedAsync();
        Refresh();
    }

    [RelayCommand]
    private static Task OpenListAsync(ListSummaryRow row) =>
        Shell.Current.GoToAsync($"{nameof(ListDetailPage)}?listId={row.ListId}");

    [RelayCommand]
    private async Task CreateListAsync()
    {
        var name = await ListNamePrompt.AskAsync(AppResources.Common_NewList, AppResources.Lists_NewPromptMessage, AppResources.Common_Create);
        if (name == null) return;

        data.UserDataRepository.CreateList(name);
        Refresh();
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private void Refresh()
    {
        var repository = data.UserDataRepository;
        var lists = repository.GetLists();

        BuiltInLists.Clear();
        foreach (var favourites in lists.Where(l => l.IsSystem))
            BuiltInLists.Add(new ListSummaryRow(favourites.Id, UiText.ListName(favourites), UiText.WordCount(favourites.Count), IconGlyphs.Favorite));
        BuiltInLists.Add(new ListSummaryRow(ListDetailViewModel.HistoryListId, AppResources.Common_History, UiText.WordCount(repository.HistoryCount), IconGlyphs.History));

        UserLists.Clear();
        foreach (var list in lists.Where(l => !l.IsSystem))
            UserLists.Add(new ListSummaryRow(list.Id, list.Name, UiText.WordCount(list.Count), IconGlyphs.Bookmarks));
        OnPropertyChanged(nameof(ShowUserListsHint));
    }
}
