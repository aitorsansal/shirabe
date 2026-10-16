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
            var name = await page.DisplayPromptAsync(title, message, accept, "Cancel", "List name", MaxLength, Keyboard.Text, value);
            if (name == null) return null;

            name = name.Trim();
            if (name.Length > 0 && !repository.IsListNameTaken(name, exceptListId))
                return name;

            message = name.Length == 0 ? "The name can't be empty." : $"There's already a list called \"{name}\".";
            value = name;
        }
    }
}
