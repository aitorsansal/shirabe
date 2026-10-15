using JapaneseDictionary.Core.Data;

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
}
