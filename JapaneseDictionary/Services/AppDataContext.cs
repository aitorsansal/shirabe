using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Services;

/// <summary>
/// Owns the app's data-layer singletons and their startup sequencing.
/// Construction is async (the bundled dictionary has to be copied out of the
/// APK asset into a real file before SQLite can open it), so this can't just
/// be a normal DI-constructed singleton — every consumer calls
/// <see cref="EnsureInitializedAsync"/> before touching the properties below.
/// Safe to call repeatedly/concurrently; only the first caller does the work.
/// </summary>
public sealed class AppDataContext : IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public DictionaryRepository DictionaryRepository { get; private set; } = null!;
    public UserDataRepository UserDataRepository { get; private set; } = null!;
    public SearchService SearchService { get; private set; } = null!;
    public DaKanjiRecognizerService Recognizer { get; } = new();

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var dictionaryPath = await DatabaseInitializer.EnsureDictionaryDatabaseAsync();
            DictionaryRepository = new DictionaryRepository(dictionaryPath);
            UserDataRepository = new UserDataRepository(DatabaseInitializer.UserDataDatabasePath);
            SearchService = new SearchService(DictionaryRepository);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        DictionaryRepository?.Dispose();
        UserDataRepository?.Dispose();
        await Recognizer.DisposeAsync();
    }
}
