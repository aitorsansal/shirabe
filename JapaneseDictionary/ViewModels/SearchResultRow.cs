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
    public static SearchResultRow FromEntry(DictionaryEntry entry, string? viaNote = null)
    {
        var firstSense = entry.Senses.FirstOrDefault();
        var tags = firstSense?.PartOfSpeechTags.Take(2)
            .Select((tag, i) => new TagChip(tag, i))
            .ToList() ?? [];
        var gloss = string.Join(" · ", entry.Senses.SelectMany(s => s.Glosses).Take(3));

        return new SearchResultRow(
            entry.Id, entry.PrimaryReading, entry.PrimaryText,
            entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon),
            tags, gloss, viaNote);
    }
}

/// <summary>One part-of-speech tag chip, colored by cycling through the
/// five-hue accent palette by index (see HueConverters.cs).</summary>
public sealed record TagChip(string Label, int HueIndex);

/// <summary>One recent-word pill on the search screen.</summary>
public sealed record RecentWordChip(int EntryId, string Word);
