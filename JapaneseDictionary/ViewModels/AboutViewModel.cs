using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

public sealed record AboutLink(string Label, string Url);

public sealed record AboutSection(string Title, string Body, IReadOnlyList<AboutLink> Links);

/// <summary>
/// Backs the About page: app name/version plus the attributions the bundled
/// data and models require. The EDRDG licence (JMdict/KANJIDIC2) specifically
/// asks for the acknowledgement on a separate screen reachable from a menu,
/// which this page is. Keep the wording in sync with DATA-LICENSE.md and
/// THIRD-PARTY-NOTICES.md.
/// </summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private const string RepoUrl = "https://github.com/aitorsansal/shirabe";

    // Also the URL given to the Play Console. Source lives in web/public/privacy/.
    private const string PrivacyPolicyUrl = "https://shirabe.aitorsansal.com/privacy/";

    public string AppName => AppInfo.Current.Name;

    public string VersionText => UiText.Format(AppResources.Settings_Version, AppInfo.Current.VersionString, AppInfo.Current.BuildString);

    // Instance (not static) so a language change, which rebuilds the pages and
    // with them this transient view model, picks up the new strings. Project and
    // licence names that are proper nouns stay untranslated.
    public IReadOnlyList<AboutSection> Sections { get; } =
    [
        new(AppResources.About_PrivacyTitle,
            AppResources.About_PrivacyBody,
            [
                new(AppResources.About_PrivacyPolicy, PrivacyPolicyUrl),
            ]),

        new(AppResources.About_OpenSourceTitle,
            AppResources.About_OpenSourceBody,
            [
                new(AppResources.About_SourceCode, RepoUrl),
                new(AppResources.About_MitLicense, $"{RepoUrl}/blob/main/LICENSE"),
            ]),

        new(AppResources.About_DictionaryTitle,
            AppResources.About_DictionaryBody,
            [
                new(AppResources.About_EdrdgLicence, "https://www.edrdg.org/edrdg/licence.html"),
                new(AppResources.About_JmdictProject, "https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project"),
                new(AppResources.About_KanjidicProject, "https://www.edrdg.org/wiki/index.php/KANJIDIC_Project"),
            ]),

        new(AppResources.About_StrokeTitle,
            AppResources.About_StrokeBody,
            [
                new("KanjiVG", "https://kanjivg.tagaini.net/"),
                new("CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
            ]),

        new(AppResources.About_SentencesTitle,
            AppResources.About_SentencesBody,
            [
                new("Tatoeba", "https://tatoeba.org/"),
                new("CC BY 2.0 FR", "https://creativecommons.org/licenses/by/2.0/fr/"),
            ]),

        new(AppResources.About_DatabaseTitle,
            AppResources.About_DatabaseBody,
            [
                new("CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/"),
                new(AppResources.About_DataLicenseDetails, $"{RepoUrl}/blob/main/DATA-LICENSE.md"),
            ]),

        new(AppResources.About_HandwritingTitle,
            AppResources.About_HandwritingBody,
            [
                new("DaKanji Single Kanji Recognition", "https://github.com/CaptainDario/DaKanji-Single-Kanji-Recognition"),
                new("ETL Character Database", "https://etlcdb.db.aist.go.jp/"),
            ]),

        new(AppResources.About_FontsTitle,
            AppResources.About_FontsBody,
            [
                new(AppResources.About_ThirdPartyNotices, $"{RepoUrl}/blob/main/THIRD-PARTY-NOTICES.md"),
            ]),
    ];

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private static async Task OpenLinkAsync(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception)
        {
            // No browser available to handle the link; nothing useful to show.
        }
    }
}
