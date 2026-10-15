using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private const string RepoUrl = "https://github.com/aitorsansal/JapaneseDictionary";

    public string AppName => AppInfo.Current.Name;

    public string VersionText => $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public IReadOnlyList<AboutSection> Sections { get; } =
    [
        new("OPEN SOURCE",
            "Free and open source software, released under the MIT license.",
            [
                new("Source code on GitHub", RepoUrl),
                new("MIT license", $"{RepoUrl}/blob/main/LICENSE"),
            ]),

        new("DICTIONARY DATA",
            "This application uses the JMdict/EDICT and KANJIDIC dictionary files. These files are the property of the Electronic Dictionary Research and Development Group, and are used in conformance with the Group's licence.",
            [
                new("EDRDG licence", "https://www.edrdg.org/edrdg/licence.html"),
                new("JMdict project", "https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project"),
                new("KANJIDIC project", "https://www.edrdg.org/wiki/index.php/KANJIDIC_Project"),
            ]),

        new("STROKE ORDER",
            "Stroke order data comes from KanjiVG, copyright Ulrich Apel, licensed under CC BY-SA 3.0.",
            [
                new("KanjiVG", "https://kanjivg.tagaini.net/"),
                new("CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0/"),
            ]),

        new("EXAMPLE SENTENCES",
            "Example sentences and their translations come from Tatoeba and its contributors, licensed under CC BY 2.0 FR.",
            [
                new("Tatoeba", "https://tatoeba.org/"),
                new("CC BY 2.0 FR", "https://creativecommons.org/licenses/by/2.0/fr/"),
            ]),

        new("DATABASE LICENSE",
            "The bundled dictionary database is derived from the sources above and is distributed under CC BY-SA 4.0.",
            [
                new("CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/"),
                new("Data license details", $"{RepoUrl}/blob/main/DATA-LICENSE.md"),
            ]),

        new("HANDWRITING RECOGNITION",
            "Character recognition powered by machine learning from Dariyooo (DaAppLab). The model was trained on the ETL Character Database (Electrotechnical Laboratory, 1973-1984, distributed by AIST) and the KanjiVG dataset.",
            [
                new("DaKanji Single Kanji Recognition", "https://github.com/CaptainDario/DaKanji-Single-Kanji-Recognition"),
                new("ETL Character Database", "https://etlcdb.db.aist.go.jp/"),
            ]),

        new("FONTS AND LIBRARIES",
            "Archivo and Noto Serif JP are used under the SIL Open Font License 1.1, Open Sans under the Apache License 2.0. ONNX Runtime, CommunityToolkit.Mvvm and sqlite-net are used under the MIT license.",
            [
                new("Full third-party notices", $"{RepoUrl}/blob/main/THIRD-PARTY-NOTICES.md"),
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
