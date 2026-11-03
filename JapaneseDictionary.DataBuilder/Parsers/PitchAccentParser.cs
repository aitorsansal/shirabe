using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>
/// Matches Kanjium's pitch accent list (accents.txt, tab-separated
/// <c>kanji, reading, accents</c>; accents are comma-separated downstep positions,
/// 0 = flat; kana-only words repeat the kana in the kanji column) to JMdict
/// entries. Kanjium has no JMdict ids, so a row belongs to an entry when its
/// kanji column is one of the entry's kanji spellings (or, for a kana-only row,
/// one of its readings) and its reading is one of the entry's readings.
/// </summary>
public static class PitchAccentParser
{
    public sealed record Result(
        Dictionary<int, List<PitchAccent>> ByEntry,
        int Rows,
        int MalformedRows,
        int UnmatchedRows,
        int KanaRowMatchesOnKanjiEntries);

    public static Result Parse(string path, IReadOnlyList<DictionaryEntry> entries)
    {
        // Spelling (kanji form or reading) -> entries having it.
        var bySpelling = new Dictionary<string, List<DictionaryEntry>>();
        void Index(string spelling, DictionaryEntry entry)
        {
            if (!bySpelling.TryGetValue(spelling, out var list)) bySpelling[spelling] = list = [];
            if (list.Count == 0 || list[^1] != entry) list.Add(entry);
        }

        foreach (var entry in entries)
        {
            foreach (var k in entry.KanjiForms) Index(k.Text, entry);
            foreach (var r in entry.Readings) Index(r.Text, entry);
        }

        // (entry, reading) -> best row so far. Rank: the headword spelling beats another
        // kanji spelling beats a kana-only row; ties keep the file's first.
        var best = new Dictionary<(int EntryId, string Reading), (int Rank, int Order, IReadOnlyList<int> Accents)>();
        int rows = 0, malformed = 0, unmatched = 0, kanaOnKanji = 0, order = 0;

        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            rows++;
            var parts = line.Split('\t');
            if (parts.Length != 3 || !TryParseAccents(parts[2], out var accents))
            {
                malformed++;
                continue;
            }

            // Kana-only words leave the reading column empty: the kana is the kanji column.
            var (kanji, reading) = (parts[0], parts[1].Length > 0 ? parts[1] : parts[0]);
            var matchedAny = false;
            if (bySpelling.TryGetValue(kanji, out var candidates))
            {
                foreach (var entry in candidates)
                {
                    if (!entry.Readings.Any(r => r.Text == reading)) continue;
                    matchedAny = true;

                    var isKanjiForm = entry.KanjiForms.Any(k => k.Text == kanji);
                    var rank = kanji == entry.PrimaryText ? 2 : isKanjiForm ? 1 : 0;
                    if (rank == 0 && entry.KanjiForms.Count > 0 && !entry.IsUsuallyKana) kanaOnKanji++;

                    var key = (entry.Id, reading);
                    if (!best.TryGetValue(key, out var current) || rank > current.Rank)
                        best[key] = (rank, order, accents);
                }
            }

            if (!matchedAny) unmatched++;
            order++;
        }

        var byEntry = new Dictionary<int, List<PitchAccent>>();
        foreach (var ((entryId, reading), value) in best.OrderBy(kv => kv.Key.EntryId).ThenBy(kv => kv.Value.Order))
        {
            if (!byEntry.TryGetValue(entryId, out var list)) byEntry[entryId] = list = [];
            list.Add(new PitchAccent(reading, value.Accents));
        }

        return new Result(byEntry, rows, malformed, unmatched, kanaOnKanji);
    }

    private static bool TryParseAccents(string text, out IReadOnlyList<int> accents)
    {
        // A few rows tag accents with a part of speech, "(副)0,(名)3": drop the tags.
        var list = new List<int>();
        foreach (var part in text.Split(','))
        {
            var number = part.Contains(')') ? part[(part.IndexOf(')') + 1)..] : part;
            if (!int.TryParse(number, out var n) || n < 0)
            {
                accents = [];
                return false;
            }

            if (!list.Contains(n)) list.Add(n);
        }

        accents = list;
        return list.Count > 0;
    }
}
