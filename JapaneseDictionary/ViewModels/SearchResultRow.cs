using JapaneseDictionary.Core.Models;

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
    string? ViaNote)
{
    public string Romaji => Core.Search.RomajiConverter.ToRomaji(Reading);

    public static SearchResultRow FromEntry(DictionaryEntry entry, string? viaNote = null)
    {
        var firstSense = entry.Senses.FirstOrDefault();
        var tags = firstSense?.PartOfSpeechTags.Take(2)
            .Select(TagChip.For)
            .ToList() ?? [];
        var gloss = string.Join(" · ", entry.MeaningGlosses.Take(3));

        return new SearchResultRow(
            entry.Id, entry.PrimaryReading, entry.PrimaryText,
            entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon),
            tags, gloss, viaNote);
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
