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
    private DictionaryEntry? entry;

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
    public ObservableCollection<Deinflector.ConjugationRow> ConjugationRows { get; } = [];

    public bool HasOtherForms => OtherForms.Count > 0;
    public string ConjugationNote { get; private set; } = "";

    public bool IsMeaningTab => SelectedTab == DetailTab.Meaning;
    public bool IsSentencesTab => SelectedTab == DetailTab.Sentences;
    public bool IsKanjiTab => SelectedTab == DetailTab.Kanji;
    public bool IsConjugationTab => SelectedTab == DetailTab.Conjugation;

    partial void OnEntryIdChanged(int value) => _ = LoadAsync();

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = Enum.Parse<DetailTab>(tabName);
        OnPropertyChanged(nameof(IsMeaningTab));
        OnPropertyChanged(nameof(IsSentencesTab));
        OnPropertyChanged(nameof(IsKanjiTab));
        OnPropertyChanged(nameof(IsConjugationTab));
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

        var entry = data.DictionaryRepository.GetEntry(EntryId);
        if (entry == null) return;
        this.entry = entry;

        EntryWord = entry.PrimaryText;
        EntryReading = entry.PrimaryReading;

        var wordClasses = data.DictionaryRepository.GetEntryWordClasses(EntryId);
        var primaryClass = wordClasses.FirstOrDefault();

        LoadBadges(entry, wordClasses);
        LoadSenseGroups(entry);
        LoadOtherForms(entry);
        LoadCompounds(entry);
        LoadExamples(entry);
        LoadKanjiCards(entry);
        LoadConjugation(entry, primaryClass);

        SelectedTab = DetailTab.Meaning;
        OnPropertyChanged(nameof(IsMeaningTab));
        OnPropertyChanged(nameof(IsSentencesTab));
        OnPropertyChanged(nameof(IsKanjiTab));
        OnPropertyChanged(nameof(IsConjugationTab));
    }

    private void LoadBadges(DictionaryEntry entry, IReadOnlyList<WordClass> wordClasses)
    {
        Badges.Clear();
        if (entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon))
            Badges.Add(new EntryBadge("common", IsCommon: true, HueIndex: 0));

        var hue = 0;
        foreach (var wc in wordClasses.Distinct())
            Badges.Add(new EntryBadge(FormatWordClass(wc), IsCommon: false, HueIndex: hue++));
        Badges.Add(new EntryBadge($"#{entry.Id}", IsCommon: false, HueIndex: hue));
    }

    private void LoadSenseGroups(DictionaryEntry entry)
    {
        SenseGroups.Clear();
        var groups = entry.Senses
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
            });

        foreach (var group in groups) SenseGroups.Add(group);
    }

    private void LoadOtherForms(DictionaryEntry entry)
    {
        OtherForms.Clear();
        var primaryKanji = entry.KanjiForms.FirstOrDefault(k => k.IsCommon)?.Text ?? entry.KanjiForms.FirstOrDefault()?.Text;
        var primaryReading = entry.Readings.FirstOrDefault(r => r.IsCommon)?.Text ?? entry.Readings.FirstOrDefault()?.Text;

        foreach (var kanji in entry.KanjiForms.Where(k => k.Text != primaryKanji))
            OtherForms.Add(new FormChip(kanji.Text, "alternate form"));
        foreach (var reading in entry.Readings.Where(r => r.Text != primaryReading))
            OtherForms.Add(new FormChip(reading.Text, "alternate reading"));

        OnPropertyChanged(nameof(HasOtherForms));
    }

    private void LoadCompounds(DictionaryEntry entry)
    {
        Compounds.Clear();
        var primaryKanji = entry.KanjiForms.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(primaryKanji)) return;

        // Best-effort "related words": other entries sharing a kanji from this
        // headword. JMdict's own curated cross-references aren't parsed into
        // the schema (see DictionaryRepository.FindByKanjiContains).
        var seen = new HashSet<int>();
        foreach (var ch in primaryKanji)
        {
            if (Compounds.Count >= CompoundsLimit) break;
            foreach (var relatedId in data.DictionaryRepository.FindByKanjiContains(ch.ToString(), entry.Id, CompoundsLimit))
            {
                if (!seen.Add(relatedId)) continue;
                var related = data.DictionaryRepository.GetEntry(relatedId);
                if (related == null) continue;

                var gloss = related.Senses.SelectMany(s => s.Glosses).FirstOrDefault() ?? "";
                Compounds.Add(new CompoundRow(related.Id, related.PrimaryText, related.PrimaryReading, gloss));
                if (Compounds.Count >= CompoundsLimit) break;
            }
        }
    }

    private void LoadExamples(DictionaryEntry entry)
    {
        Examples.Clear();
        var sentences = data.DictionaryRepository.GetExampleSentences(entry.Id, ExamplesLimit);
        var highlightCandidates = entry.KanjiForms.Select(k => k.Text)
            .Concat(entry.Readings.Select(r => r.Text))
            .OrderByDescending(s => s.Length) // prefer the longest/most specific match
            .ToList();

        foreach (var sentence in sentences)
        {
            var hitText = highlightCandidates.FirstOrDefault(h => sentence.Japanese.Contains(h));
            if (hitText == null)
            {
                Examples.Add(new ExampleCard(sentence.Id, sentence.Japanese, "", "", sentence.English));
                continue;
            }

            var index = sentence.Japanese.IndexOf(hitText, StringComparison.Ordinal);
            Examples.Add(new ExampleCard(
                sentence.Id,
                sentence.Japanese[..index],
                hitText,
                sentence.Japanese[(index + hitText.Length)..],
                sentence.English));
        }
    }

    private void LoadKanjiCards(DictionaryEntry entry)
    {
        KanjiCards.Clear();
        var characters = (entry.KanjiForms.FirstOrDefault()?.Text ?? "")
            .Where(IsKanji)
            .Select(c => c.ToString())
            .Distinct();

        foreach (var character in characters)
        {
            var info = data.DictionaryRepository.GetKanjiInfo(character);
            if (info == null) continue;

            var strokes = data.DictionaryRepository.GetKanjiStrokes(character).Select(s => s.SvgPath).ToList();
            KanjiCards.Add(new KanjiCard
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
    }

    private void LoadConjugation(DictionaryEntry entry, WordClass wordClass)
    {
        ConjugationRows.Clear();
        if (wordClass == WordClass.Other)
        {
            ShowConjugationTab = false;
            return;
        }

        var rows = Deinflector.GetConjugationTable(entry.PrimaryReading, wordClass);
        foreach (var row in rows) ConjugationRows.Add(row);

        ShowConjugationTab = ConjugationRows.Count > 0;
        ConjugationNote = ShowConjugationTab
            ? $"Generated from the entry's word class — {FormatWordClass(wordClass)}."
            : "";
        OnPropertyChanged(nameof(ConjugationNote));
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
