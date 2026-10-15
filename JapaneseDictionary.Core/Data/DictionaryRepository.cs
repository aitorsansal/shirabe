using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;

using SQLite;

namespace JapaneseDictionary.Core.Data;

/// <summary>
/// Read-only access to the bundled dictionary SQLite file (built by
/// JapaneseDictionary.DataBuilder). One instance per app lifetime is enough
/// — sqlite-net-pcl's <see cref="SQLiteConnection"/> is not itself
/// thread-safe for concurrent writers, but this class only ever reads.
/// </summary>
public sealed class DictionaryRepository : IDisposable
{
    private readonly SQLiteConnection db;

    public DictionaryRepository(string databasePath)
    {
        db = new SQLiteConnection(databasePath, SQLiteOpenFlags.ReadOnly);
    }

    public void Dispose() => db.Dispose();

    /// <summary>Entry ids whose kanji form or reading exactly equals <paramref name="text"/>.</summary>
    public IReadOnlyList<int> FindByExactText(string text)
    {
        var ids = new HashSet<int>();
        ids.UnionWith(db.Table<KanjiFormRow>().Where(r => r.Text == text).Select(r => r.EntryId));
        ids.UnionWith(db.Table<ReadingFormRow>().Where(r => r.Text == text).Select(r => r.EntryId));
        return ids.ToList();
    }

    /// <summary>Entry ids whose kanji form or reading starts with <paramref name="prefix"/>,
    /// most-common-first, capped at <paramref name="limit"/>.</summary>
    public IReadOnlyList<int> FindByPrefix(string prefix, int limit)
    {
        var ids = new List<(int EntryId, bool IsCommon)>();

        ids.AddRange(db.Table<KanjiFormRow>()
            .Where(r => r.Text.StartsWith(prefix))
            .Select(r => new ValueTuple<int, bool>(r.EntryId, r.IsCommon)));
        ids.AddRange(db.Table<ReadingFormRow>()
            .Where(r => r.Text.StartsWith(prefix))
            .Select(r => new ValueTuple<int, bool>(r.EntryId, r.IsCommon)));

        return ids
            .GroupBy(x => x.EntryId)
            .OrderByDescending(g => g.Any(x => x.IsCommon))
            .Select(g => g.Key)
            .Take(limit)
            .ToList();
    }

    /// <summary>Other entries whose kanji form contains <paramref name="text"/> —
    /// a best-effort "related words" list. JMdict's own curated cross-reference
    /// data (&lt;xref&gt;) isn't parsed into the schema, so this approximates it
    /// by kanji overlap instead.</summary>
    public IReadOnlyList<int> FindByKanjiContains(string text, int excludeEntryId, int limit) =>
        db.Table<KanjiFormRow>()
            .Where(r => r.Text.Contains(text) && r.EntryId != excludeEntryId)
            .Select(r => r.EntryId)
            .Distinct()
            .Take(limit)
            .ToList();

    /// <summary>Entry ids with at least one gloss containing <paramref name="text"/> (case-insensitive).</summary>
    public IReadOnlyList<int> FindByGlossContains(string text, int limit)
    {
        // SQLite's LIKE operator is case-insensitive for ASCII by default, which
        // covers English gloss text without needing an explicit .ToLower() call
        // (sqlite-net-pcl's LINQ translator doesn't support that reliably anyway).
        return db.Table<GlossRow>()
            .Where(g => g.Text.Contains(text))
            .Join(db.Table<SenseRow>(), g => g.SenseId, s => s.Id, (g, s) => s.EntryId)
            .Distinct()
            .Take(limit)
            .ToList();
    }

    /// <summary>Every reading text in the dictionary starting with <paramref name="stem"/>,
    /// paired with its entry id and word classes — used by the deinflection
    /// matcher to find verb/adjective candidates near a conjugated query.</summary>
    public IReadOnlyList<(int EntryId, string Reading, IReadOnlyList<WordClass> WordClasses)> FindReadingCandidates(string stem, int limit)
    {
        // Most-common-first (matching FindByPrefix's convention) so that a common
        // verb like 聞く isn't silently excluded from the capped candidate window
        // by an otherwise-arbitrary (unordered) row order.
        var readingRows = db.Table<ReadingFormRow>()
            .Where(r => r.Text.StartsWith(stem))
            .OrderByDescending(r => r.IsCommon)
            .Take(limit)
            .ToList();

        if (readingRows.Count == 0) return [];

        // sqlite-net-pcl's TableQuery doesn't translate .Join() to SQL — it falls
        // back to Enumerable.Join, which fully materializes the *entire* other
        // table (every WordEntryRow, 442k+ rows) into memory on every call. Scope
        // the WordEntryRow lookup to just the candidate entry ids instead (IN clause).
        var entryIds = readingRows.Select(r => r.EntryId).Distinct().ToList();
        var classesByEntryId = db.Table<WordEntryRow>()
            .Where(w => entryIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.WordClassesJoined);

        var result = new List<(int, string, IReadOnlyList<WordClass>)>(readingRows.Count);
        foreach (var row in readingRows)
        {
            if (!classesByEntryId.TryGetValue(row.EntryId, out var joined)) continue;
            var classes = ParseWordClasses(joined);
            if (classes.Count == 0) continue; // not a conjugating word, irrelevant to deinflection
            result.Add((row.EntryId, row.Text, classes));
        }

        return result;
    }

    public DictionaryEntry? GetEntry(int id)
    {
        var wordRow = db.Find<WordEntryRow>(id);
        if (wordRow == null) return null;

        var kanjiForms = db.Table<KanjiFormRow>().Where(r => r.EntryId == id)
            .Select(r => new KanjiForm(r.Text, r.IsCommon)).ToList();
        var readings = db.Table<ReadingFormRow>().Where(r => r.EntryId == id)
            .Select(r => new ReadingForm(r.Text, r.IsCommon, true, [])).ToList();

        var senseRows = db.Table<SenseRow>().Where(s => s.EntryId == id).OrderBy(s => s.SenseOrder).ToList();
        var senses = senseRows.Select(s =>
        {
            var glosses = db.Table<GlossRow>().Where(g => g.SenseId == s.Id).OrderBy(g => g.GlossOrder)
                .Select(g => g.Text).ToList();
            return new WordSense(
                ParseWordClasses(""), // per-sense class breakdown isn't preserved separately from the entry-level roll-up; see WordEntryRow.WordClassesJoined
                Split(s.PartOfSpeechTagsJoined),
                glosses,
                Split(s.FieldsJoined),
                Split(s.MiscTagsJoined));
        }).ToList();

        return new DictionaryEntry(id, kanjiForms, readings, senses);
    }

    public KanjiInfo? GetKanjiInfo(string character)
    {
        var row = db.Find<KanjiRow>(character);
        if (row == null) return null;
        return new KanjiInfo(
            row.Character, row.StrokeCount, row.Grade, row.JlptLevel, row.FrequencyRank,
            row.RadicalNumber, row.SkipCode, Split(row.OnReadingsJoined), Split(row.KunReadingsJoined), Split(row.MeaningsJoined));
    }

    public IReadOnlyList<(int StrokeIndex, string SvgPath)> GetKanjiStrokes(string character) =>
        db.Table<KanjiStrokeRow>().Where(r => r.Character == character).OrderBy(r => r.StrokeIndex)
            .Select(r => (r.StrokeIndex, r.SvgPath)).ToList();

    public IReadOnlyList<ExampleSentence> GetExampleSentences(int entryId, int limit)
    {
        var sentenceIds = db.Table<ExampleSentenceLinkRow>().Where(l => l.EntryId == entryId)
            .Take(limit).Select(l => l.SentenceId).ToList();

        return sentenceIds.Select(id => db.Find<ExampleSentenceRow>(id))
            .Where(r => r != null)
            .Select(r => new ExampleSentence(r!.Id, r.Japanese, r.English, [entryId]))
            .ToList();
    }

    private static List<WordClass> ParseWordClasses(string joined) =>
        Split(joined).Select(s => Enum.TryParse<WordClass>(s, out var wc) ? wc : WordClass.Other)
            .Where(wc => wc != WordClass.Other).ToList();

    /// <summary>Every WordClass on the entry as a whole (rolled up across all
    /// senses at build time) — the granularity the deinflection matcher needs.</summary>
    public IReadOnlyList<WordClass> GetEntryWordClasses(int entryId)
    {
        var row = db.Find<WordEntryRow>(entryId);
        return row == null ? [] : ParseWordClasses(row.WordClassesJoined);
    }

    private static List<string> Split(string joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split(';').ToList();
}
