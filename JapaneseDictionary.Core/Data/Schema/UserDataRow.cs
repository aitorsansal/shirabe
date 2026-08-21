using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>One dictionary entry the user has looked up. Re-viewing an entry
/// updates <see cref="ViewedAtUtc"/> in place rather than adding a duplicate
/// row, so history reads as a most-recently-viewed list, not a full log.</summary>
public sealed class HistoryEntryRow
{
    [PrimaryKey]
    public int EntryId { get; set; }

    [Indexed]
    public long ViewedAtUtc { get; set; } // Unix seconds — sqlite-net has no native DateTime index support
}

/// <summary>One dictionary entry the user has starred for quick access.</summary>
public sealed class FavoriteEntryRow
{
    [PrimaryKey]
    public int EntryId { get; set; }

    public long AddedAtUtc { get; set; }
}
