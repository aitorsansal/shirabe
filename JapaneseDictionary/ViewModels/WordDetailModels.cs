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

/// <param name="Tags">The part-of-speech chips the group's senses share; empty
/// for senses without any, which show <paramref name="FallbackTitle"/> instead.</param>
public sealed record SenseGroup(IReadOnlyList<TagChip> Tags, string FallbackTitle, IReadOnlyList<SenseItem> Items)
{
    public bool HasTags => Tags.Count > 0;
    public bool HasNoTags => Tags.Count == 0;
}

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

/// <summary>One cell of the conjugation table: the displayed form split into the
/// stem shared by the whole table (shown highlighted) and the conjugated ending,
/// plus its kana reading and romaji. The reading line only shows when the
/// displayed form isn't already kana.</summary>
public sealed record ConjugationTableCell(string Stem, string Ending, string Reading, string Romaji)
{
    public static readonly ConjugationTableCell Missing = new("", "—", "", "");

    public bool HasReading => Reading.Length > 0;
}

/// <summary>A conjugation table row.</summary>
public sealed record ConjugationTableRow(string Label, string Meaning, ConjugationTableCell Plain, ConjugationTableCell Polite)
{
    public bool HasMeaning => Meaning.Length > 0;

    /// <summary>The prefix every displayed form in the table shares: 食 for 食べる
    /// (食べない, 食べます), 行 for 行く. Empty when nothing is shared, as for 来る
    /// in kana (くる, こない, きた).</summary>
    public static string CommonStem(IEnumerable<Deinflector.ConjugationDisplayRow> rows)
    {
        string? stem = null;
        foreach (var form in rows.SelectMany(r => new[] { r.Plain, r.Polite }).Where(c => c != null).Select(c => c!.Display))
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

    public static ConjugationTableRow From(Deinflector.ConjugationDisplayRow row, string stem, bool isAdjective) =>
        new(UiText.ConjugationLabel(row.Label), UiText.ConjugationMeaning(row.Label, isAdjective),
            Cell(row.Plain, stem), Cell(row.Polite, stem));

    private static ConjugationTableCell Cell(Deinflector.ConjugationCell? cell, string stem)
    {
        if (cell == null) return ConjugationTableCell.Missing;

        var (head, ending) = cell.Display.StartsWith(stem, StringComparison.Ordinal)
            ? (stem, cell.Display[stem.Length..])
            : ("", cell.Display);
        return new ConjugationTableCell(
            head, ending,
            cell.HasReading ? cell.Reading : "",
            RomajiConverter.ToRomaji(cell.Reading));
    }
}
