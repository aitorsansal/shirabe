using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Data.Schema;

namespace JapaneseDictionary.Tests;

public class UserDataRepositoryTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"userdata_test_{Guid.NewGuid():N}.db3");
    private readonly UserDataRepository repo;

    public UserDataRepositoryTests()
    {
        repo = new UserDataRepository(dbPath);
    }

    public void Dispose()
    {
        repo.Dispose();
        File.Delete(dbPath);
    }

    [Fact]
    public void RecordView_AppearsInRecentHistory()
    {
        repo.RecordView(1358280);
        Assert.Contains(1358280, repo.GetRecentHistory(10));
    }

    [Fact]
    public void RecordView_SameEntryTwice_DoesNotDuplicate()
    {
        repo.RecordView(1358280);
        repo.RecordView(1358280);
        Assert.Single(repo.GetRecentHistory(10));
    }

    [Fact]
    public void RecordView_MostRecentFirst()
    {
        repo.RecordView(1);
        repo.RecordView(2);
        repo.RecordView(3);
        Assert.Equal([3, 2, 1], repo.GetRecentHistory(10));
    }

    [Fact]
    public void ClearHistory_RemovesAllEntries()
    {
        repo.RecordView(1);
        repo.ClearHistory();
        Assert.Empty(repo.GetRecentHistory(10));
    }

    [Fact]
    public void AddFavorite_MarksAsFavorite()
    {
        repo.AddFavorite(1358280);
        Assert.True(repo.IsFavorite(1358280));
        Assert.Contains(1358280, repo.GetFavorites());
    }

    [Fact]
    public void RemoveFavorite_UnmarksFavorite()
    {
        repo.AddFavorite(1358280);
        repo.RemoveFavorite(1358280);
        Assert.False(repo.IsFavorite(1358280));
    }

    [Fact]
    public void AddFavorite_Twice_DoesNotThrow()
    {
        repo.AddFavorite(1358280);
        repo.AddFavorite(1358280);
        Assert.Single(repo.GetFavorites());
    }

    [Fact]
    public void GetLists_FreshDatabase_HasOnlyFavourites()
    {
        var list = Assert.Single(repo.GetLists());
        Assert.Equal(UserDataRepository.FavoritesListId, list.Id);
        Assert.True(list.IsSystem);
    }

    [Fact]
    public void CreateList_AppearsAfterFavouritesWithCount()
    {
        var id = repo.CreateList("  JLPT N5  ");
        repo.AddToList(id, 1);
        repo.AddToList(id, 2);

        var lists = repo.GetLists();
        Assert.Equal(UserDataRepository.FavoritesListId, lists[0].Id);
        Assert.Equal(new WordListSummary(id, "JLPT N5", false, 2), lists[1]);
    }

    [Fact]
    public void CreateList_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => repo.CreateList("   "));
    }

    [Fact]
    public void IsListNameTaken_IgnoresCaseAndSelf()
    {
        var id = repo.CreateList("Verbs");
        Assert.True(repo.IsListNameTaken("verbs"));
        Assert.True(repo.IsListNameTaken("favourites"));
        Assert.False(repo.IsListNameTaken("VERBS", exceptListId: id));
    }

    [Fact]
    public void AddToList_Twice_DoesNotDuplicate()
    {
        var id = repo.CreateList("Verbs");
        repo.AddToList(id, 1);
        repo.AddToList(id, 1);
        Assert.Single(repo.GetListEntries(id));
    }

    [Fact]
    public void GetListEntries_MostRecentlyAddedFirst()
    {
        var id = repo.CreateList("Verbs");
        repo.AddToList(id, 1);
        repo.AddToList(id, 2);
        repo.AddToList(id, 3);
        Assert.Equal([3, 2, 1], repo.GetListEntries(id));
    }

    [Fact]
    public void GetListIdsContaining_ReturnsEveryListWithEntry()
    {
        var a = repo.CreateList("A");
        var b = repo.CreateList("B");
        repo.CreateList("C");
        repo.AddToList(a, 1);
        repo.AddToList(b, 1);
        repo.AddFavorite(1);

        Assert.Equal(new HashSet<int> { UserDataRepository.FavoritesListId, a, b }, repo.GetListIdsContaining(1));
    }

    [Fact]
    public void RemoveFromList_LeavesOtherListsAlone()
    {
        var id = repo.CreateList("Verbs");
        repo.AddToList(id, 1);
        repo.AddFavorite(1);
        repo.RemoveFromList(id, 1);

        Assert.Empty(repo.GetListEntries(id));
        Assert.True(repo.IsFavorite(1));
    }

    [Fact]
    public void RenameList_ChangesName()
    {
        var id = repo.CreateList("Verbs");
        repo.RenameList(id, "Godan verbs");
        Assert.Equal("Godan verbs", repo.GetList(id)!.Name);
    }

    [Fact]
    public void DeleteList_RemovesListAndItsEntries()
    {
        var id = repo.CreateList("Verbs");
        repo.AddToList(id, 1);
        repo.DeleteList(id);

        Assert.Null(repo.GetList(id));
        Assert.Empty(repo.GetListEntries(id));
    }

    [Fact]
    public void Favourites_CannotBeRenamedOrDeleted()
    {
        Assert.Throws<InvalidOperationException>(() => repo.RenameList(UserDataRepository.FavoritesListId, "x"));
        Assert.Throws<InvalidOperationException>(() => repo.DeleteList(UserDataRepository.FavoritesListId));
    }

    [Fact]
    public void Reopen_KeepsListsAndDoesNotDuplicateFavourites()
    {
        var id = repo.CreateList("Verbs");
        repo.AddToList(id, 1);
        repo.Dispose();

        using var reopened = new UserDataRepository(dbPath);
        Assert.Equal(2, reopened.GetLists().Count);
        Assert.Equal([1], reopened.GetListEntries(id));
    }

    [Fact]
    public void LegacyFavoritesTable_IsMigratedOldestFirstThenDropped()
    {
        repo.Dispose();
        var legacyPath = Path.Combine(Path.GetTempPath(), $"userdata_legacy_{Guid.NewGuid():N}.db3");
        try
        {
            using (var raw = new SQLite.SQLiteConnection(legacyPath))
            {
                raw.CreateTable<FavoriteEntryRow>();
                raw.Insert(new FavoriteEntryRow { EntryId = 10, AddedAtUtc = 100 });
                raw.Insert(new FavoriteEntryRow { EntryId = 20, AddedAtUtc = 200 });
            }

            using (var migrated = new UserDataRepository(legacyPath))
                Assert.Equal([20, 10], migrated.GetFavorites());

            using var check = new SQLite.SQLiteConnection(legacyPath);
            Assert.Empty(check.GetTableInfo(nameof(FavoriteEntryRow)));
        }
        finally
        {
            File.Delete(legacyPath);
        }
    }
}
