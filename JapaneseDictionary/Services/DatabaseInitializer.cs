using System.IO.Compression;

namespace JapaneseDictionary.Services;

/// <summary>
/// Ensures the bundled dictionary database is available as a real file on
/// disk. SQLite needs an actual file path, not a stream, so the read-only
/// asset bundled at build time (<c>dictionary.db3.br</c>, Brotli-compressed
/// from <c>Resources/Raw/dictionary.db3</c> by the CompressDictionaryDatabase
/// target, only reachable via <see cref="FileSystem.OpenAppPackageFileAsync"/>)
/// has to be decompressed into app-writable storage before Core's
/// DictionaryRepository can open it. The copy is redone whenever the bundled data version
/// (<c>dictionary.version</c>, written by DataBuilder) differs from the one
/// recorded next to the installed copy, so app updates that ship new data
/// replace the old database instead of keeping it forever.
/// </summary>
public static class DatabaseInitializer
{
    private const string DictionaryFileName = "dictionary.db3";
    private const string CompressedDictionaryAsset = DictionaryFileName + ".br";
    private const string DictionaryVersionAsset = "dictionary.version";

    public static string DictionaryDatabasePath => Path.Combine(FileSystem.AppDataDirectory, DictionaryFileName);

    public static string InstalledVersionPath => DictionaryDatabasePath + ".version";

    public static string UserDataDatabasePath => Path.Combine(FileSystem.AppDataDirectory, "userdata.db3");

    public static async Task<bool> IsDictionaryUpToDateAsync() =>
        File.Exists(DictionaryDatabasePath) && await ReadInstalledVersionAsync() == await ReadBundledVersionAsync();

    public static async Task<string> EnsureDictionaryDatabaseAsync()
    {
        var destination = DictionaryDatabasePath;
        if (await IsDictionaryUpToDateAsync())
            return destination;

        var bundledVersion = await ReadBundledVersionAsync();

        // The APK asset stream reads synchronously under the hood, and Brotli
        // decoding is CPU-bound, so the copy runs on a worker thread to keep the UI
        // (and its "preparing" state) alive. It goes to a temporary file that is
        // swapped in afterwards: if the process is killed mid-copy, a truncated file
        // must never sit at the real path, and the version is only recorded once the
        // swap has happened.
        await Task.Run(async () =>
        {
            var temporary = destination + ".tmp";
            await using (var assetStream = await FileSystem.OpenAppPackageFileAsync(CompressedDictionaryAsset))
            await using (var brotliStream = new BrotliStream(assetStream, CompressionMode.Decompress))
            await using (var fileStream = File.Create(temporary))
            {
                await brotliStream.CopyToAsync(fileStream, bufferSize: 1 << 20);
            }

            File.Move(temporary, destination, overwrite: true);
            await File.WriteAllTextAsync(InstalledVersionPath, bundledVersion);
        });

        return destination;
    }

    private static async Task<string> ReadBundledVersionAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(DictionaryVersionAsset);
        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync()).Trim();
    }

    private static async Task<string?> ReadInstalledVersionAsync()
    {
        if (!File.Exists(InstalledVersionPath))
            return null;

        return (await File.ReadAllTextAsync(InstalledVersionPath)).Trim();
    }
}
