using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

/// <summary>One list in the add-to-list sheet; tapping toggles membership.</summary>
public sealed partial class ListToggleRow : ObservableObject
{
    public required int ListId { get; init; }
    public required string Name { get; init; }

    [ObservableProperty]
    private bool isMember;
}

/// <summary>
/// Backs the add-to-list sheet. Changes apply immediately on tap (no
/// confirm step), so closing the sheet any way — DONE, back, backdrop —
/// never loses a choice.
/// </summary>
public sealed partial class AddToListViewModel : ObservableObject
{
    private readonly AppDataContext data;
    private int entryId;

    public AddToListViewModel(AppDataContext data)
    {
        this.data = data;
    }

    public ObservableCollection<ListToggleRow> Lists { get; } = [];

    [ObservableProperty]
    private string word = "";

    public async Task LoadAsync(int entryId, string word)
    {
        this.entryId = entryId;
        Word = word;
        await data.EnsureInitializedAsync();
        Refresh();
    }

    [RelayCommand]
    private void Toggle(ListToggleRow row)
    {
        if (row.IsMember) data.UserDataRepository.RemoveFromList(row.ListId, entryId);
        else data.UserDataRepository.AddToList(row.ListId, entryId);
        row.IsMember = !row.IsMember;
    }

    [RelayCommand]
    private async Task CreateListAsync()
    {
        var name = await ListNamePrompt.AskAsync(AppResources.Common_NewList, AppResources.Lists_NewPromptFromWord, AppResources.Common_Create);
        if (name == null) return;

        var listId = data.UserDataRepository.CreateList(name);
        data.UserDataRepository.AddToList(listId, entryId);
        Refresh();
    }

    private void Refresh()
    {
        var memberOf = data.UserDataRepository.GetListIdsContaining(entryId);
        Lists.Clear();
        foreach (var list in data.UserDataRepository.GetLists())
            Lists.Add(new ListToggleRow { ListId = list.Id, Name = UiText.ListName(list), IsMember = memberOf.Contains(list.Id) });
    }
}
