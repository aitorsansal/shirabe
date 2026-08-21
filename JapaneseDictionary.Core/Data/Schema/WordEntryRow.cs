using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>
/// SQLite row for one dictionary entry (JMdict ent_seq). Denormalized
/// headline fields (<see cref="PrimaryText"/>/<see cref="PrimaryReading"/>)
/// live here for fast result-list rendering without joining out to
/// <see cref="KanjiFormRow"/>/<see cref="ReadingFormRow"/>; those tables hold
/// every form for search matching.
/// </summary>
public sealed class WordEntryRow
{
    [PrimaryKey]
    public int Id { get; set; }

    public string PrimaryText { get; set; } = "";
    public string PrimaryReading { get; set; } = "";
    public bool IsCommon { get; set; }

    /// <summary>Semicolon-joined <see cref="Models.WordClass"/> names this entry's
    /// senses carry, e.g. "IchidanVerb". Empty for non-conjugating words.
    /// Used by the deinflection matcher to know which ending rules to try
    /// against a candidate headword.</summary>
    public string WordClassesJoined { get; set; } = "";
}

/// <summary>One kanji spelling of an entry, one row per form. Indexed on
/// <see cref="Text"/> for exact/prefix kanji search.</summary>
public sealed class KanjiFormRow
{
    [PrimaryKey, AutoIncrement]
    public int RowId { get; set; }

    [Indexed]
    public int EntryId { get; set; }

    [Indexed]
    public string Text { get; set; } = "";

    public bool IsCommon { get; set; }
}

/// <summary>One reading of an entry, one row per form. Indexed on
/// <see cref="Text"/> for exact/prefix kana search and for the deinflection
/// matcher's nearby-candidate scan.</summary>
public sealed class ReadingFormRow
{
    [PrimaryKey, AutoIncrement]
    public int RowId { get; set; }

    [Indexed]
    public int EntryId { get; set; }

    [Indexed]
    public string Text { get; set; } = "";

    public bool IsCommon { get; set; }
}
