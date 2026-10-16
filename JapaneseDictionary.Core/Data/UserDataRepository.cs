using JapaneseDictionary.Core.Data.Schema;

using SQLite;

namespace JapaneseDictionary.Core.Data;

/// <summary>A list as shown on the lists screen.</summary>
public sealed record WordListSummary(int Id, string Name, bool IsSystem, int Count);

/// <summary>
/// Local, on-device history and word lists — a separate writable SQLite file
/// from the read-only bundled <c>dictionary.db3</c>, so it survives app
/// updates (which replace the bundled asset) and stays untouched by them.
/// </summary>
public sealed class UserDataRepository : IDisposable
{
    /// <summary>History is trimmed to this many most-recently-viewed entries.</summary>
    private const int MaxHistoryEntries = 200;

    /// <summary>The Favourites system list, always present.</summary>
    public const int FavoritesListId = 1;

    private const string FavoritesListName = "Favourites";

    private readonly SQLiteConnection db;

    /// <summary>Guarantees strictly increasing view timestamps even if two
    /// <see cref="RecordView"/> calls land in the same millisecond, so
    /// "most recent first" ordering is never ambiguous.</summary>
    private long lastViewTimestampMs;

    public UserDataRepository(string databasePath)
    {
        db = new SQLiteConnection(databasePath);
        db.CreateTable<HistoryEntryRow>();
        db.CreateTable<WordListRow>();
        db.CreateTable<WordListEntryRow>();
        EnsureFavoritesList();
        MigrateLegacyFavorites();
    }

    public void Dispose() => db.Dispose();

    public void RecordView(int entryId)
    {
        var now = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), lastViewTimestampMs + 1);
        lastViewTimestampMs = now;
        db.InsertOrReplace(new HistoryEntryRow { EntryId = entryId, ViewedAtUtc = now });
        TrimHistory();
    }

    public IReadOnlyList<int> GetRecentHistory(int limit) =>
        db.Table<HistoryEntryRow>().OrderByDescending(r => r.ViewedAtUtc).Take(limit).Select(r => r.EntryId).ToList();

    public int HistoryCount => db.Table<HistoryEntryRow>().Count();

    public void ClearHistory() => db.DeleteAll<HistoryEntryRow>();

    public void AddFavorite(int entryId) => AddToList(FavoritesListId, entryId);

    public void RemoveFavorite(int entryId) => RemoveFromList(FavoritesListId, entryId);

    public bool IsFavorite(int entryId) => IsInList(FavoritesListId, entryId);

    public IReadOnlyList<int> GetFavorites() => GetListEntries(FavoritesListId);

    /// <summary>All lists, Favourites first, then user lists oldest first.</summary>
    public IReadOnlyList<WordListSummary> GetLists()
    {
        var counts = db.Query<ListCount>("SELECT ListId, COUNT(*) AS Count FROM WordListEntryRow GROUP BY ListId")
            .ToDictionary(c => c.ListId, c => c.Count);

        return db.Table<WordListRow>()
            .OrderByDescending(l => l.IsSystem)
            .ThenBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToList()
            .Select(l => new WordListSummary(l.Id, l.Name, l.IsSystem, counts.GetValueOrDefault(l.Id)))
            .ToList();
    }

    public WordListSummary? GetList(int listId) => GetLists().FirstOrDefault(l => l.Id == listId);

    /// <summary>Case-insensitive, so the add-to-list sheet never shows two
    /// lists that look the same.</summary>
    public bool IsListNameTaken(string name, int? exceptListId = null)
    {
        var trimmed = name.Trim();
        return db.Table<WordListRow>().ToList()
            .Any(l => l.Id != exceptListId && string.Equals(l.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public int CreateList(string name)
    {
        var trimmed = RequireName(name);
        var row = new WordListRow { Name = trimmed, CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        db.Insert(row);
        return row.Id;
    }

    public void RenameList(int listId, string name)
    {
        var trimmed = RequireName(name);
        var row = RequireUserList(listId);
        row.Name = trimmed;
        db.Update(row);
    }

    public void DeleteList(int listId)
    {
        RequireUserList(listId);
        db.RunInTransaction(() =>
        {
            db.Execute("DELETE FROM WordListEntryRow WHERE ListId = ?", listId);
            db.Delete<WordListRow>(listId);
        });
    }

    public void AddToList(int listId, int entryId)
    {
        if (IsInList(listId, entryId)) return;
        db.Insert(new WordListEntryRow { ListId = listId, EntryId = entryId, AddedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    public void RemoveFromList(int listId, int entryId) =>
        db.Execute("DELETE FROM WordListEntryRow WHERE ListId = ? AND EntryId = ?", listId, entryId);

    public bool IsInList(int listId, int entryId) =>
        db.Table<WordListEntryRow>().Any(r => r.ListId == listId && r.EntryId == entryId);

    /// <summary>Entry ids in the list, most recently added first. RowId is
    /// monotonic, so it breaks ties between same-millisecond adds.</summary>
    public IReadOnlyList<int> GetListEntries(int listId) =>
        db.Table<WordListEntryRow>().Where(r => r.ListId == listId).OrderByDescending(r => r.RowId).Select(r => r.EntryId).ToList();

    public IReadOnlySet<int> GetListIdsContaining(int entryId) =>
        db.Table<WordListEntryRow>().Where(r => r.EntryId == entryId).Select(r => r.ListId).ToHashSet();

    private void EnsureFavoritesList()
    {
        if (db.Find<WordListRow>(FavoritesListId) != null) return;
        db.Insert(new WordListRow { Id = FavoritesListId, Name = FavoritesListName, IsSystem = true });
    }

    /// <summary>Moves rows from the pre-lists favorites table into the
    /// Favourites list, oldest first so RowId order matches add order.</summary>
    private void MigrateLegacyFavorites()
    {
        if (db.GetTableInfo(nameof(FavoriteEntryRow)).Count == 0) return;

        db.RunInTransaction(() =>
        {
            foreach (var legacy in db.Table<FavoriteEntryRow>().OrderBy(r => r.AddedAtUtc).ToList())
                AddToList(FavoritesListId, legacy.EntryId);
            db.DropTable<FavoriteEntryRow>();
        });
    }

    private static string RequireName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("List name can't be empty.", nameof(name));
        return trimmed;
    }

    private WordListRow RequireUserList(int listId)
    {
        var row = db.Find<WordListRow>(listId) ?? throw new ArgumentException($"No list with id {listId}.", nameof(listId));
        if (row.IsSystem) throw new InvalidOperationException("System lists can't be renamed or deleted.");
        return row;
    }

    private void TrimHistory()
    {
        var count = db.Table<HistoryEntryRow>().Count();
        if (count <= MaxHistoryEntries) return;

        var toRemove = db.Table<HistoryEntryRow>()
            .OrderBy(r => r.ViewedAtUtc)
            .Take(count - MaxHistoryEntries)
            .Select(r => r.EntryId)
            .ToList();

        foreach (var id in toRemove)
            db.Delete<HistoryEntryRow>(id);
    }

    private sealed class ListCount
    {
        public int ListId { get; set; }
        public int Count { get; set; }
    }
}
