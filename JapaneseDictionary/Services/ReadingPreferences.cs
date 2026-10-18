using CommunityToolkit.Mvvm.ComponentModel;

namespace JapaneseDictionary.Services;

public sealed class ReadingPreferences : ObservableObject
{
    public static ReadingPreferences Instance { get; } = new();
    private bool showRomaji = Preferences.Default.Get("show_romaji", true);

    public bool ShowRomaji
    {
        get => showRomaji;
        set
        {
            if (SetProperty(ref showRomaji, value)) Preferences.Default.Set("show_romaji", value);
        }
    }
}
