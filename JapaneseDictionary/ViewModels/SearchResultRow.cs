using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

/// <summary>One entry row (search results, list contents) — display-ready,
/// already hydrated from the matching <see cref="DictionaryEntry"/>.</summary>
public sealed record SearchResultRow(
    int EntryId,
    string Reading,
    string Word,
    bool IsCommon,
    IReadOnlyList<TagChip> Tags,
    string Gloss,
    string? ViaNote,
    int? JlptLevel = null)
{
    public string Romaji => Core.Search.RomajiConverter.ToRomaji(Reading);

    public bool HasJlpt => JlptLevel != null;

    public string JlptLabel => JlptLevel is { } level ? UiText.Format(AppResources.Common_JlptBadge, level) : "";

    private const int PreviewGlossCount = 3;

    /// <param name="matchedGloss">For an English search, the gloss the entry matched
    /// on. When it isn't among the first glosses it leads the preview, so 足 found by
    /// "leg" reads "leg · foot · paw" instead of hiding why it's in the list.</param>
    public static SearchResultRow FromEntry(DictionaryEntry entry, int? jlptLevel = null, string? viaNote = null, string? matchedGloss = null)
    {
        var firstSense = entry.Senses.FirstOrDefault();
        var tags = firstSense?.PartOfSpeechTags.Take(2)
            .Select(TagChip.For)
            .ToList() ?? [];

        var glosses = entry.MeaningGlosses.ToList();
        var preview = glosses.Take(PreviewGlossCount).ToList();
        if (matchedGloss != null && !preview.Contains(matchedGloss) && glosses.Contains(matchedGloss))
            preview = [matchedGloss, .. glosses.Where(g => g != matchedGloss).Take(PreviewGlossCount - 1)];
        var gloss = string.Join(" · ", preview);

        return new SearchResultRow(
            entry.Id, entry.PrimaryReading, entry.PrimaryText,
            entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon),
            tags, gloss, viaNote, jlptLevel);
    }
}

/// <summary>One part-of-speech tag chip. Its color comes from the tag's family
/// (noun, verb, adjective, adverb, other), through the five-hue accent palette
/// (see HueConverters.cs).</summary>
public sealed record TagChip(string Label, int HueIndex)
{
    public static TagChip For(string tag) => new(tag, HueOf(PartOfSpeech.GroupOf(tag)));

    // Hue0..Hue4 = green, lime, yellow, blue, purple.
    private static int HueOf(PartOfSpeechGroup group) => group switch
    {
        PartOfSpeechGroup.Verb => 0,
        PartOfSpeechGroup.Other => 1,
        PartOfSpeechGroup.Adjective => 2,
        PartOfSpeechGroup.Noun => 3,
        PartOfSpeechGroup.Adverb => 4,
        _ => 1,
    };
}

/// <summary>One recent-word pill on the search screen.</summary>
public sealed record RecentWordChip(int EntryId, string Word);
