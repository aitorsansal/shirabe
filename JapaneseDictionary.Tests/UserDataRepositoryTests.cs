using JapaneseDictionary.Core.Data;

namespace JapaneseDictionary.Tests;

public class UserDataRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"userdata_test_{Guid.NewGuid():N}.db3");
    private readonly UserDataRepository _repo;

    public UserDataRepositoryTests()
    {
        _repo = new UserDataRepository(_dbPath);
    }

    public void Dispose()
    {
        _repo.Dispose();
        File.Delete(_dbPath);
    }

    [Fact]
    public void RecordView_AppearsInRecentHistory()
    {
        _repo.RecordView(1358280);
        Assert.Contains(1358280, _repo.GetRecentHistory(10));
    }

    [Fact]
    public void RecordView_SameEntryTwice_DoesNotDuplicate()
    {
        _repo.RecordView(1358280);
        _repo.RecordView(1358280);
        Assert.Single(_repo.GetRecentHistory(10));
    }

    [Fact]
    public void RecordView_MostRecentFirst()
    {
        _repo.RecordView(1);
        _repo.RecordView(2);
        _repo.RecordView(3);
        Assert.Equal([3, 2, 1], _repo.GetRecentHistory(10));
    }

    [Fact]
    public void ClearHistory_RemovesAllEntries()
    {
        _repo.RecordView(1);
        _repo.ClearHistory();
        Assert.Empty(_repo.GetRecentHistory(10));
    }

    [Fact]
    public void AddFavorite_MarksAsFavorite()
    {
        _repo.AddFavorite(1358280);
        Assert.True(_repo.IsFavorite(1358280));
        Assert.Contains(1358280, _repo.GetFavorites());
    }

    [Fact]
    public void RemoveFavorite_UnmarksFavorite()
    {
        _repo.AddFavorite(1358280);
        _repo.RemoveFavorite(1358280);
        Assert.False(_repo.IsFavorite(1358280));
    }

    [Fact]
    public void AddFavorite_Twice_DoesNotThrow()
    {
        _repo.AddFavorite(1358280);
        _repo.AddFavorite(1358280);
        Assert.Single(_repo.GetFavorites());
    }
}
