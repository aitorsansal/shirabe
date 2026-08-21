namespace JapaneseDictionary.Services;

/// <summary>
/// Ensures the bundled dictionary database is available as a real file on
/// disk. SQLite needs an actual file path, not a stream, so the read-only
/// asset bundled at build time (<c>Resources/Raw/dictionary.db3</c>, only
/// reachable via <see cref="FileSystem.OpenAppPackageFileAsync"/>) has to be
/// copied into app-writable storage once before Core's DictionaryRepository
/// can open it.
/// </summary>
public static class DatabaseInitializer
{
    private const string DictionaryAsset = "dictionary.db3";

    public static async Task<string> EnsureDictionaryDatabaseAsync()
    {
        var destination = Path.Combine(FileSystem.AppDataDirectory, DictionaryAsset);
        if (!File.Exists(destination))
        {
            using var assetStream = await FileSystem.OpenAppPackageFileAsync(DictionaryAsset);
            using var fileStream = File.Create(destination);
            await assetStream.CopyToAsync(fileStream);
        }

        return destination;
    }

    public static string UserDataDatabasePath => Path.Combine(FileSystem.AppDataDirectory, "userdata.db3");
}
