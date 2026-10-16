using CommunityToolkit.Mvvm.ComponentModel;
using JapaneseDictionary.Core.Search;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

public enum DetailTab { Meaning, Sentences, Kanji, Conjugation }

/// <summary>One badge in the entry header (common/JLPT/word-class/id). The
/// "common" badge gets a fixed lime fill (see Colors.xaml's CommonBadgeFill);
/// every other badge cycles through the five-hue accent palette by
/// <see cref="HueIndex"/> (see HueConverters.cs).</summary>
public sealed record EntryBadge(string Label, bool IsCommon, int HueIndex);

public sealed record SenseItem(int Number, string Gloss, string? Note);

public sealed record SenseGroup(string PartOfSpeech, IReadOnlyList<SenseItem> Items);

public sealed record FormChip(string Word, string Note);

public sealed record CompoundRow(int? EntryId, string Word, string Reading, string Gloss);

public sealed record ExampleCard(long Id, string Pre, string Hit, string Post, string English)
{
    public string Japanese => Pre + Hit + Post;
}

public partial class KanjiCard : ObservableObject
{
    public required string Character { get; init; }
    public required string Meanings { get; init; }
    public required string OnReadings { get; init; }
    public required string KunReadings { get; init; }
    public required string GradeLabel { get; init; }
    public required string FreqLabel { get; init; }
    public required IReadOnlyList<string> StrokePaths { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrokeLabel), nameof(StepLabel), nameof(ProgressFraction), nameof(VisiblePaths))]
    private int currentStep;

    public int TotalStrokes => StrokePaths.Count;
    public string StrokeLabel => UiText.Format(AppResources.Word_Strokes, TotalStrokes);
    public string StepLabel => CurrentStep == 0 ? StrokeLabel : UiText.Format(AppResources.Word_StrokeStep, CurrentStep, TotalStrokes);
    public double ProgressFraction => TotalStrokes == 0 ? 0 : (double)CurrentStep / TotalStrokes;
    public IReadOnlyList<string> VisiblePaths => CurrentStep == 0 ? [] : StrokePaths.Take(CurrentStep).ToList();

    public void Step() => CurrentStep = CurrentStep % TotalStrokes + 1;
}

/// <summary>A conjugation table row with each form split into the stem shared by
/// the whole table (shown highlighted) and the conjugated ending.</summary>
public sealed record ConjugationTableRow(string Label, string PlainStem, string PlainEnding, string PoliteStem, string PoliteEnding)
{
    /// <summary>The prefix every form in the table shares: い for 行く (いく,
    /// いった, いかない), たべ for 食べる, べんきょう for 勉強. Empty for 来る, whose
    /// stem changes too (くる, こない, きた).</summary>
    public static string CommonStem(IEnumerable<Deinflector.ConjugationRow> rows)
    {
        string? stem = null;
        foreach (var form in rows.SelectMany(r => new[] { r.Plain, r.Polite }).Where(f => f != "—"))
        {
            if (stem is null)
            {
                stem = form;
                continue;
            }

            var length = 0;
            while (length < stem.Length && length < form.Length && stem[length] == form[length])
                length++;
            stem = stem[..length];
        }

        return stem ?? "";
    }

    public static ConjugationTableRow From(Deinflector.ConjugationRow row, string stem)
    {
        var (plainStem, plainEnding) = Split(row.Plain, stem);
        var (politeStem, politeEnding) = Split(row.Polite, stem);
        return new ConjugationTableRow(UiText.ConjugationLabel(row.Label), plainStem, plainEnding, politeStem, politeEnding);
    }

    private static (string Stem, string Ending) Split(string form, string stem) =>
        form.StartsWith(stem, StringComparison.Ordinal) ? (stem, form[stem.Length..]) : ("", form);
}
