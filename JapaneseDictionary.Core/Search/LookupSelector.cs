namespace JapaneseDictionary.Core.Search;

/// <summary>Selects a lookup result using searches of the written form and its conjugated reading.</summary>
public static class LookupSelector
{
    public static int? PickEntry(IReadOnlyList<int> formResultIds, IReadOnlyList<int>? readingResultIds)
    {
        if (formResultIds.Count == 0) return null;

        if (readingResultIds is { Count: > 0 })
        {
            var readingIds = readingResultIds.ToHashSet();
            foreach (var id in formResultIds)
                if (readingIds.Contains(id)) return id;
        }

        return formResultIds[0];
    }

    public static int? PickEntry(SearchService search, string query, string? reading = null)
    {
        var formIds = search.Search(query, limit: 20).Select(r => r.EntryId).ToList();
        var readingIds = !string.IsNullOrEmpty(reading) && reading != query && ContainsKanji(query)
            ? search.Search(reading, limit: 50).Select(r => r.EntryId).ToList()
            : null;

        return PickEntry(formIds, readingIds);
    }

    private static bool ContainsKanji(string text) =>
        text.EnumerateRunes().Any(r => r.Value is >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF
            or >= 0x20000 and <= 0x323AF);
}
