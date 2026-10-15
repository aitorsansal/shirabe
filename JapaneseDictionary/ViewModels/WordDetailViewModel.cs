using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

[QueryProperty(nameof(EntryId), "entryId")]
public partial class WordDetailViewModel : ObservableObject
{
    private const int CompoundsLimit = 8;
    private const int ExamplesLimit = 20;

    private readonly AppDataContext data;

    public WordDetailViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private int entryId;

    [ObservableProperty]
    private string entryWord = "";

    [ObservableProperty]
    private string entryReading = "";

    [ObservableProperty]
    private DetailTab selectedTab = DetailTab.Meaning;

    [ObservableProperty]
    private bool showConjugationTab;

    public ObservableCollection<EntryBadge> Badges { get; } = [];
    public ObservableCollection<SenseGroup> SenseGroups { get; } = [];
    public ObservableCollection<FormChip> OtherForms { get; } = [];
    public ObservableCollection<CompoundRow> Compounds { get; } = [];
    public ObservableCollection<ExampleCard> Examples { get; } = [];
    public ObservableCollection<KanjiCard> KanjiCards { get; } = [];
    public ObservableCollection<ConjugationTableRow> ConjugationRows { get; } = [];

    public bool HasOtherForms => OtherForms.Count > 0;
    public string ConjugationNote { get; private set; } = "";

    public bool IsMeaningTab => SelectedTab == DetailTab.Meaning;
    public bool IsSentencesTab => SelectedTab == DetailTab.Sentences;
    public bool IsKanjiTab => SelectedTab == DetailTab.Kanji;
    public bool IsConjugationTab => SelectedTab == DetailTab.Conjugation;

    // The Meaning tab is built on open; the others only the first time they're
    // selected, so opening a word doesn't lay out three hidden tabs.
    private DictionaryEntry? loadedEntry;
    private IReadOnlyList<ConjugationTableRow> conjugationTable = [];
    private bool examplesLoaded;
    private bool kanjiLoaded;
    private bool conjugationLoaded;

    private sealed record MeaningContent(
        DictionaryEntry Entry,
        List<EntryBadge> Badges,
        List<SenseGroup> SenseGroups,
        List<FormChip> OtherForms,
        List<CompoundRow> Compounds,
        IReadOnlyList<ConjugationTableRow> ConjugationTable,
        string ConjugationNote);

    partial void OnEntryIdChanged(int value) => _ = LoadAsync();

    // Concurrent so the tab buttons don't disable themselves while a tab loads.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SelectTab(string tabName)
    {
        SelectedTab = Enum.Parse<DetailTab>(tabName);
        OnPropertyChanged(nameof(IsMeaningTab));
        OnPropertyChanged(nameof(IsSentencesTab));
        OnPropertyChanged(nameof(IsKanjiTab));
        OnPropertyChanged(nameof(IsConjugationTab));

        await LoadTabContentAsync(SelectedTab);
    }

    [RelayCommand]
    private async Task OpenCompoundAsync(CompoundRow compound)
    {
        if (compound.EntryId is { } id)
        {
            data.UserDataRepository.RecordView(id);
            await Shell.Current.GoToAsync($"{nameof(WordDetailPage)}?entryId={id}");
        }
        // No linked entry id (a compound outside the dictionary we resolved,
        // e.g. found only by kanji overlap with no exact match) — nothing to
        // navigate to; the row simply isn't tappable in that case (see XAML).
    }

    [RelayCommand]
    private static void StepStroke(KanjiCard card) => card.Step();

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    private async Task LoadAsync()
    {
        await data.EnsureInitializedAsync();

        var entryId = EntryId;
        var content = await Task.Run(() => BuildMeaningContent(entryId));
        if (content is null) return;

        loadedEntry = content.Entry;
        EntryWord = content.Entry.PrimaryText;
        EntryReading = content.Entry.PrimaryReading;
        Fill(Badges, content.Badges);
        Fill(SenseGroups, content.SenseGroups);
        Fill(OtherForms, content.OtherForms);
        OnPropertyChanged(nameof(HasOtherForms));
        Fill(Compounds, content.Compounds);

        conjugationTable = content.ConjugationTable;
        ShowConjugationTab = conjugationTable.Count > 0;
        ConjugationNote = content.ConjugationNote;
        OnPropertyChanged(nameof(ConjugationNote));

        if (SelectedTab != DetailTab.Meaning)
            await LoadTabContentAsync(SelectedTab);
    }

    private async Task LoadTabContentAsync(DetailTab tab)
    {
        if (loadedEntry is not { } entry) return;

        switch (tab)
        {
            case DetailTab.Sentences when !examplesLoaded:
                examplesLoaded = true;
                Fill(Examples, await Task.Run(() => BuildExamples(entry)));
                break;
            case DetailTab.Kanji when !kanjiLoaded:
                kanjiLoaded = true;
                Fill(KanjiCards, await Task.Run(() => BuildKanjiCards(entry)));
                break;
            case DetailTab.Conjugation when !conjugationLoaded:
                conjugationLoaded = true;
                Fill(ConjugationRows, conjugationTable);
                break;
        }
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private MeaningContent? BuildMeaningContent(int entryId)
    {
        var entry = data.DictionaryRepository.GetEntry(entryId);
        if (entry == null) return null;

        var wordClasses = data.DictionaryRepository.GetEntryWordClasses(entryId);
        var primaryClass = wordClasses.FirstOrDefault();
        var conjugationTable = BuildConjugationTable(entry, primaryClass);

        return new MeaningContent(
            entry,
            BuildBadges(entry, wordClasses),
            BuildSenseGroups(entry),
            BuildOtherForms(entry),
            BuildCompounds(entry),
            conjugationTable,
            conjugationTable.Count > 0 ? $"Generated from the entry's word class — {FormatWordClass(primaryClass)}." : "");
    }

    private static List<EntryBadge> BuildBadges(DictionaryEntry entry, IReadOnlyList<WordClass> wordClasses)
    {
        var badges = new List<EntryBadge>();
        if (entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon))
            badges.Add(new EntryBadge("common", IsCommon: true, HueIndex: 0));

        var hue = 0;
        foreach (var wc in wordClasses.Distinct())
            badges.Add(new EntryBadge(FormatWordClass(wc), IsCommon: false, HueIndex: hue++));
        badges.Add(new EntryBadge($"#{entry.Id}", IsCommon: false, HueIndex: hue));
        return badges;
    }

    private static List<SenseGroup> BuildSenseGroups(DictionaryEntry entry) =>
        entry.Senses
            .GroupBy(s => s.PartOfSpeechTags.Count > 0 ? string.Join(", ", s.PartOfSpeechTags) : "Other")
            .Select(g =>
            {
                var items = new List<SenseItem>();
                foreach (var sense in g)
                {
                    foreach (var gloss in sense.Glosses)
                    {
                        var note = sense.MiscTags.Count > 0 && items.Count == 0 ? string.Join(", ", sense.MiscTags) : null;
                        items.Add(new SenseItem(items.Count + 1, gloss, note));
                    }
                }

                return new SenseGroup(g.Key.ToUpperInvariant(), items);
            })
            .ToList();

    private static List<FormChip> BuildOtherForms(DictionaryEntry entry)
    {
        var primaryKanji = entry.KanjiForms.FirstOrDefault(k => k.IsCommon)?.Text ?? entry.KanjiForms.FirstOrDefault()?.Text;
        var primaryReading = entry.Readings.FirstOrDefault(r => r.IsCommon)?.Text ?? entry.Readings.FirstOrDefault()?.Text;

        return entry.KanjiForms.Where(k => k.Text != primaryKanji).Select(k => new FormChip(k.Text, "alternate form"))
            .Concat(entry.Readings.Where(r => r.Text != primaryReading).Select(r => new FormChip(r.Text, "alternate reading")))
            .ToList();
    }

    private List<CompoundRow> BuildCompounds(DictionaryEntry entry)
    {
        var compounds = new List<CompoundRow>();
        var primaryKanji = entry.KanjiForms.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(primaryKanji)) return compounds;

        // Best-effort "related words": other entries sharing a kanji from this
        // headword. JMdict's own curated cross-references aren't parsed into
        // the schema (see DictionaryRepository.FindByKanjiContains).
        var seen = new HashSet<int>();
        foreach (var ch in primaryKanji)
        {
            if (compounds.Count >= CompoundsLimit) break;
            foreach (var relatedId in data.DictionaryRepository.FindByKanjiContains(ch.ToString(), entry.Id, CompoundsLimit))
            {
                if (!seen.Add(relatedId)) continue;
                var related = data.DictionaryRepository.GetEntry(relatedId);
                if (related == null) continue;

                var gloss = related.Senses.SelectMany(s => s.Glosses).FirstOrDefault() ?? "";
                compounds.Add(new CompoundRow(related.Id, related.PrimaryText, related.PrimaryReading, gloss));
                if (compounds.Count >= CompoundsLimit) break;
            }
        }

        return compounds;
    }

    private List<ExampleCard> BuildExamples(DictionaryEntry entry)
    {
        var sentences = data.DictionaryRepository.GetExampleSentences(entry.Id, ExamplesLimit);
        var highlightCandidates = entry.KanjiForms.Select(k => k.Text)
            .Concat(entry.Readings.Select(r => r.Text))
            .OrderByDescending(s => s.Length) // prefer the longest/most specific match
            .ToList();

        var examples = new List<ExampleCard>();
        foreach (var sentence in sentences)
        {
            var hitText = highlightCandidates.FirstOrDefault(h => sentence.Japanese.Contains(h));
            if (hitText == null)
            {
                examples.Add(new ExampleCard(sentence.Id, sentence.Japanese, "", "", sentence.English));
                continue;
            }

            var index = sentence.Japanese.IndexOf(hitText, StringComparison.Ordinal);
            examples.Add(new ExampleCard(
                sentence.Id,
                sentence.Japanese[..index],
                hitText,
                sentence.Japanese[(index + hitText.Length)..],
                sentence.English));
        }

        return examples;
    }

    private List<KanjiCard> BuildKanjiCards(DictionaryEntry entry)
    {
        var characters = (entry.KanjiForms.FirstOrDefault()?.Text ?? "")
            .Where(IsKanji)
            .Select(c => c.ToString())
            .Distinct();

        var cards = new List<KanjiCard>();
        foreach (var character in characters)
        {
            var info = data.DictionaryRepository.GetKanjiInfo(character);
            if (info == null) continue;

            var strokes = data.DictionaryRepository.GetKanjiStrokes(character).Select(s => s.SvgPath).ToList();
            cards.Add(new KanjiCard
            {
                Character = character,
                Meanings = string.Join(", ", info.Meanings),
                OnReadings = info.OnReadings.Count > 0 ? string.Join("、", info.OnReadings) : "—",
                KunReadings = info.KunReadings.Count > 0 ? string.Join("、", info.KunReadings) : "—",
                GradeLabel = info.Grade is { } g ? $"grade {g}" : "ungraded",
                FreqLabel = info.FrequencyRank is { } f ? $"freq #{f}" : "freq —",
                StrokePaths = strokes,
            });
        }

        return cards;
    }

    private static IReadOnlyList<ConjugationTableRow> BuildConjugationTable(DictionaryEntry entry, WordClass wordClass)
    {
        if (wordClass == WordClass.Other) return [];

        var rows = Deinflector.GetConjugationTable(entry.PrimaryReading, wordClass);
        var stem = ConjugationTableRow.CommonStem(rows);
        return rows.Select(row => ConjugationTableRow.From(row, stem)).ToList();
    }

    private static bool IsKanji(char c) => c is >= '一' and <= '鿿';

    private static string FormatWordClass(WordClass wc) => wc switch
    {
        WordClass.IchidanVerb => "ichidan verb",
        WordClass.IAdjective => "i-adjective",
        WordClass.NaAdjective => "na-adjective",
        WordClass.SuruVerb => "suru verb",
        WordClass.KuruVerb => "kuru verb",
        WordClass.GodanVerbU or WordClass.GodanVerbKu or WordClass.GodanVerbGu or WordClass.GodanVerbSu
            or WordClass.GodanVerbTsu or WordClass.GodanVerbNu or WordClass.GodanVerbBu or WordClass.GodanVerbMu
            or WordClass.GodanVerbRu or WordClass.GodanVerbIkuYuku => "godan verb",
        _ => "word",
    };
}
