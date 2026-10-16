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

/// <summary>Pre-lists favorites table. Only read once, to migrate its rows
/// into the Favourites system list, then dropped.</summary>
public sealed class FavoriteEntryRow
{
    [PrimaryKey]
    public int EntryId { get; set; }

    public long AddedAtUtc { get; set; }
}

/// <summary>A named word list. System lists (Favourites) can't be renamed or
/// deleted.</summary>
public sealed class WordListRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public bool IsSystem { get; set; }
    public long CreatedAtUtc { get; set; }
}

/// <summary>Membership of one entry in one list. The composite unique index
/// keeps an entry from being added to the same list twice.</summary>
public sealed class WordListEntryRow
{
    [PrimaryKey, AutoIncrement]
    public int RowId { get; set; }

    [Indexed(Name = "UX_WordListEntry", Order = 1, Unique = true)]
    public int ListId { get; set; }

    [Indexed(Name = "UX_WordListEntry", Order = 2, Unique = true)]
    public int EntryId { get; set; }

    public long AddedAtUtc { get; set; }
}
