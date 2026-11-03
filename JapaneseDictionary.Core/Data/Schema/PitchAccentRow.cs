using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>Pitch accent of one reading of an entry (from Kanjium). One row per
/// (entry, reading).</summary>
public sealed class PitchAccentRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int EntryId { get; set; }

    public string Reading { get; set; } = "";

    /// <summary>Comma-joined downstep positions in source order, e.g. "0,3".</summary>
    public string AccentsJoined { get; set; } = "";
}
