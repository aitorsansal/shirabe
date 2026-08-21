using CommunityToolkit.Mvvm.ComponentModel;

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

public sealed record ExampleCard(long Id, string Pre, string Hit, string Post, string English);

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
    public string StrokeLabel => $"{TotalStrokes} strokes";
    public string StepLabel => CurrentStep == 0 ? StrokeLabel : $"stroke {CurrentStep} / {TotalStrokes}";
    public double ProgressFraction => TotalStrokes == 0 ? 0 : (double)CurrentStep / TotalStrokes;
    public IReadOnlyList<string> VisiblePaths => CurrentStep == 0 ? [] : StrokePaths.Take(CurrentStep).ToList();

    public void Step() => CurrentStep = CurrentStep % TotalStrokes + 1;
}
