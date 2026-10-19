using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Services;

/// <summary>Tapping a part-of-speech chip explains its code. A static command
/// so the chip template needs no binding to whichever view model owns the list.</summary>
public static class TagInfo
{
    public static IAsyncRelayCommand<TagChip> ShowCommand { get; } = new AsyncRelayCommand<TagChip>(ShowAsync);

    private static async Task ShowAsync(TagChip? chip)
    {
        if (chip == null || Shell.Current == null) return;
        await Shell.Current.DisplayAlertAsync(
            chip.Label.ToUpperInvariant(), UiText.PartOfSpeechName(chip.Label), AppResources.Common_Done);
    }
}
