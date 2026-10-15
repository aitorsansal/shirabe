using Microsoft.Extensions.DependencyInjection;

using JapaneseDictionary.Views;

namespace JapaneseDictionary.Services;

/// <summary>
/// Opens a word's detail page on request from outside the app's own navigation
/// (the Look Up popup's "Open in Shirabe"). If the Shell isn't up yet (cold
/// start), the request waits until the search page first appears.
/// </summary>
public static class EntryLinks
{
    private static int? pendingEntryId;

    public static void Open(int entryId)
    {
        if (Shell.Current?.CurrentPage is null)
        {
            pendingEntryId = entryId;
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await NavigateAsync(entryId));
    }

    /// <summary>Opens the entry requested before the Shell existed, if any.</summary>
    public static async Task OpenPendingAsync()
    {
        if (pendingEntryId is not { } entryId) return;
        pendingEntryId = null;
        await NavigateAsync(entryId);
    }

    private static async Task NavigateAsync(int entryId)
    {
        var data = App.Services.GetRequiredService<AppDataContext>();
        await data.EnsureInitializedAsync();
        data.UserDataRepository.RecordView(entryId);
        await Shell.Current.GoToAsync($"{nameof(WordDetailPage)}?entryId={entryId}");
    }
}
