namespace JapaneseDictionary.ViewModels;

/// <summary>One row in the search results list — display-ready, already
/// hydrated from the matching <see cref="Core.Models.DictionaryEntry"/>.</summary>
public sealed record SearchResultRow(
    int EntryId,
    string Reading,
    string Word,
    bool IsCommon,
    IReadOnlyList<TagChip> Tags,
    string Gloss,
    string? ViaNote);

/// <summary>One part-of-speech tag chip, colored by cycling through the
/// five-hue accent palette by index (see HueConverters.cs).</summary>
public sealed record TagChip(string Label, int HueIndex);

/// <summary>One recent-word pill on the search screen.</summary>
public sealed record RecentWordChip(int EntryId, string Word);
