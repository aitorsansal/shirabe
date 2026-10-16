using JapaneseDictionary.Resources.Localization;

namespace JapaneseDictionary.Services;

/// <summary>
/// The one prompt for naming a list, shared by "new list" (add-to-list sheet,
/// lists screen) and "rename". Re-asks until the name is non-empty and not
/// already used by another list; returns null if the user cancels.
/// </summary>
public static class ListNamePrompt
{
    private const int MaxLength = 40;

    public static async Task<string?> AskAsync(string title, string message, string accept, string initialValue = "", int? exceptListId = null)
    {
        var repository = App.Services.GetRequiredService<AppDataContext>().UserDataRepository;
        var page = Shell.Current;
        var value = initialValue;

        while (true)
        {
            var name = await page.DisplayPromptAsync(title, message, accept, AppResources.Common_Cancel, AppResources.Lists_NamePlaceholder, MaxLength, Keyboard.Text, value);
            if (name == null) return null;

            name = name.Trim();
            // Favourites is stored under its English name but shown translated, so
            // both spellings count as taken.
            var taken = repository.IsListNameTaken(name, exceptListId)
                || string.Equals(name, AppResources.Common_Favourites, StringComparison.OrdinalIgnoreCase);
            if (name.Length > 0 && !taken)
                return name;

            message = name.Length == 0 ? AppResources.Lists_NameEmpty : UiText.Format(AppResources.Lists_NameTaken, name);
            value = name;
        }
    }
}
