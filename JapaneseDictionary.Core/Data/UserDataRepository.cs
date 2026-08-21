using JapaneseDictionary.Core.Data.Schema;

using SQLite;

namespace JapaneseDictionary.Core.Data;

/// <summary>
/// Local, on-device history and favorites — a separate writable SQLite file
/// from the read-only bundled <c>dictionary.db3</c>, so it survives app
/// updates (which replace the bundled asset) and stays untouched by them.
/// </summary>
public sealed class UserDataRepository : IDisposable
{
    /// <summary>History is trimmed to this many most-recently-viewed entries.</summary>
    private const int MaxHistoryEntries = 200;

    private readonly SQLiteConnection _db;

    /// <summary>Guarantees strictly increasing view timestamps even if two
    /// <see cref="RecordView"/> calls land in the same millisecond, so
    /// "most recent first" ordering is never ambiguous.</summary>
    private long _lastViewTimestampMs;

    public UserDataRepository(string databasePath)
    {
        _db = new SQLiteConnection(databasePath);
        _db.CreateTable<HistoryEntryRow>();
        _db.CreateTable<FavoriteEntryRow>();
    }

    public void Dispose() => _db.Dispose();

    public void RecordView(int entryId)
    {
        var now = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _lastViewTimestampMs + 1);
        _lastViewTimestampMs = now;
        _db.InsertOrReplace(new HistoryEntryRow { EntryId = entryId, ViewedAtUtc = now });
        TrimHistory();
    }

    public IReadOnlyList<int> GetRecentHistory(int limit) =>
        _db.Table<HistoryEntryRow>().OrderByDescending(r => r.ViewedAtUtc).Take(limit).Select(r => r.EntryId).ToList();

    public void ClearHistory() => _db.DeleteAll<HistoryEntryRow>();

    public void AddFavorite(int entryId)
    {
        if (IsFavorite(entryId)) return;
        _db.Insert(new FavoriteEntryRow { EntryId = entryId, AddedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
    }

    public void RemoveFavorite(int entryId) => _db.Delete<FavoriteEntryRow>(entryId);

    public bool IsFavorite(int entryId) => _db.Find<FavoriteEntryRow>(entryId) != null;

    public IReadOnlyList<int> GetFavorites() =>
        _db.Table<FavoriteEntryRow>().OrderByDescending(r => r.AddedAtUtc).Select(r => r.EntryId).ToList();

    private void TrimHistory()
    {
        var count = _db.Table<HistoryEntryRow>().Count();
        if (count <= MaxHistoryEntries) return;

        var toRemove = _db.Table<HistoryEntryRow>()
            .OrderBy(r => r.ViewedAtUtc)
            .Take(count - MaxHistoryEntries)
            .Select(r => r.EntryId)
            .ToList();

        foreach (var id in toRemove)
            _db.Delete<HistoryEntryRow>(id);
    }
}
