using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Services;

/// <summary>
/// Which extra meaning/sentence languages (see <see cref="TranslationLanguages"/>)
/// the user wants shown next to English. Read each time a word page opens, so a
/// change in Settings applies without a restart. On first run it defaults to the
/// device language when we carry it (Catalan counts as Spanish).
/// </summary>
public static class TranslationPreferences
{
    private const string Key = "translation_languages";

    public static IReadOnlyList<string> Selected
    {
        get
        {
            if (!Preferences.Default.ContainsKey(Key))
            {
                var defaults = TranslationLanguages.DefaultFor(LanguageService.SystemCulture.Name);
                Save(defaults);
                return defaults;
            }

            var stored = Preferences.Default.Get(Key, "");
            return TranslationLanguages.Normalize(stored.Split(',', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public static bool IsSelected(string code) => Selected.Contains(code);

    public static void Set(string code, bool selected)
    {
        var codes = Selected.ToHashSet();
        if (selected) codes.Add(code); else codes.Remove(code);
        Save(TranslationLanguages.Normalize(codes));
    }

    private static void Save(IEnumerable<string> codes) =>
        Preferences.Default.Set(Key, string.Join(',', codes));
}
