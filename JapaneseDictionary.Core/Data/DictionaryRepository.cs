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

    /// <summary>Other entries whose headword contains <paramref name="kanji"/>,
    /// best first — a "related words" list. JMdict's own curated cross-reference
    /// data (&lt;xref&gt;) isn't parsed into the schema, so this approximates it
    /// by kanji overlap instead.
    ///
    /// Matches the headword (<see cref="WordEntryRow.PrimaryText"/>), not every
    /// kanji spelling: rare alternate spellings otherwise pull in entries that
    /// show no sign of the kanji (くっ付く via 食っ付く). Ranked common words
    /// first, then words starting with the kanji (食事 before 月食), then
    /// shortest, so the list leads with everyday compounds rather than whatever
    /// the table scan hits first.</summary>
    public IReadOnlyList<int> FindRelatedByKanji(string kanji, int excludeEntryId, int limit) =>
        db.QueryScalars<int>("""
            SELECT Id FROM WordEntryRow
            WHERE PrimaryText LIKE ? AND Id != ?
            ORDER BY IsCommon DESC, (PrimaryText LIKE ?) DESC, LENGTH(PrimaryText), Id
            LIMIT ?
            """, $"%{kanji}%", excludeEntryId, $"{kanji}%", limit);

    /// <summary>Entries whose headword contains <paramref name="kanji"/> somewhere
    /// after its first character (銀行 for 行), common words first, then shortest.
    /// Headwords that start with it are left to <see cref="FindByPrefix"/>, which
    /// the search runs first; including them here would let hundreds of them
    /// crowd the actual compounds out of the limit.</summary>
    public IReadOnlyList<int> FindCompoundsByKanji(string kanji, int limit) =>
        db.QueryScalars<int>("""
            SELECT Id FROM WordEntryRow
            WHERE PrimaryText LIKE ? AND PrimaryText NOT LIKE ?
            ORDER BY IsCommon DESC, LENGTH(PrimaryText), Id
            LIMIT ?
            """, $"%{kanji}%", $"{kanji}%", limit);

    /// <summary>Every gloss containing <paramref name="text"/> (case-insensitive),
    /// unranked and uncapped — <see cref="Search.GlossRanker"/> orders them. With
    /// <paramref name="wholeWordOnly"/>, only glosses where it stands as a word.</summary>
    public IReadOnlyList<GlossCandidateRow> FindGlossCandidates(string text, bool wholeWordOnly = false)
    {
        // Raw SQL because sqlite-net-pcl's .Join() isn't translated to SQL: it
        // falls back to LINQ-to-objects and loads the whole SenseRow table.
        // SQLite's LIKE is case-insensitive for ASCII, which covers English glosses.
        // The whole-word form pads the gloss with spaces and turns brackets and
        // separators into spaces, so "go (somewhere)" matches "% go %".
        const string select = """
            SELECT s.EntryId, g.Text AS GlossText, s.SenseOrder, g.GlossOrder, w.IsCommon, w.PrimaryText
            FROM GlossRow g
            JOIN SenseRow s ON s.Id = g.SenseId
            JOIN WordEntryRow w ON w.Id = s.EntryId
            """;
        const string substring = select + """

            WHERE g.Text LIKE ? ESCAPE '\'
            """;
        const string wholeWord = select + """

            WHERE g.Text LIKE ? ESCAPE '\'
                AND (' ' || REPLACE(REPLACE(REPLACE(REPLACE(g.Text, '(', ' '), ')', ' '), ',', ' '), ';', ' ') || ' ')
                    LIKE ? ESCAPE '\'
            """;

        // The plain substring LIKE goes first so the costlier whole-word check
        // only runs on glosses that already contain the text.
        var escaped = EscapeLike(text);
        return wholeWordOnly
            ? db.Query<GlossCandidateRow>(wholeWord, $"%{escaped}%", $"% {escaped} %")
            : db.Query<GlossCandidateRow>(substring, $"%{escaped}%");
    }

    /// <summary>KANJIDIC2 newspaper frequency rank (1 = most frequent) for every
    /// kanji that has one.</summary>
    public IReadOnlyDictionary<char, int> GetKanjiFrequencyRanks() =>
        db.Query<KanjiRow>("SELECT Character, FrequencyRank FROM KanjiRow WHERE FrequencyRank IS NOT NULL")
            .Where(k => k.Character.Length == 1)
            .ToDictionary(k => k.Character[0], k => k.FrequencyRank!.Value);

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    /// <summary>Every entry with a kanji form or reading exactly equal to one of
    /// <paramref name="texts"/>, with the entry-level fields the deinflection
    /// matcher needs to check word class and rank. Two indexed IN queries, so
    /// dozens of candidate texts cost about as much as one.</summary>
    public IReadOnlyList<ExactTextMatch> FindByExactTexts(IReadOnlyCollection<string> texts)
    {
        if (texts.Count == 0) return [];
        var textList = texts.ToList();

        var formRows = db.Table<KanjiFormRow>().Where(r => textList.Contains(r.Text))
            .Select(r => (r.Text, r.EntryId, r.IsCommon)).ToList();
        formRows.AddRange(db.Table<ReadingFormRow>().Where(r => textList.Contains(r.Text))
            .Select(r => (r.Text, r.EntryId, r.IsCommon)));
        if (formRows.Count == 0) return [];

        // sqlite-net-pcl's TableQuery doesn't translate .Join() to SQL (it would
        // load the whole WordEntryRow table), so fetch the entries by id instead.
        var entryIds = formRows.Select(r => r.EntryId).Distinct().ToList();
        var entries = db.Table<WordEntryRow>().Where(w => entryIds.Contains(w.Id)).ToDictionary(w => w.Id);

        return formRows
            .GroupBy(r => (r.Text, r.EntryId))
            .Where(g => entries.ContainsKey(g.Key.EntryId))
            .Select(g =>
            {
                var entry = entries[g.Key.EntryId];
                return new ExactTextMatch(g.Key.Text, g.Key.EntryId, ParseWordClasses(entry.WordClassesJoined), entry.IsCommon, entry.PrimaryText, g.Any(r => r.IsCommon));
            })
            .ToList();
    }

    // Entries with no linked sentence are left out of the result.
    public IReadOnlyDictionary<int, int> GetExampleCounts(IReadOnlyCollection<int> entryIds)
    {
        if (entryIds.Count == 0) return new Dictionary<int, int>();
        var placeholders = string.Join(",", entryIds.Select(_ => "?"));
        return db.Query<ExampleCountRow>(
                $"SELECT EntryId, COUNT(*) AS Count FROM ExampleSentenceLinkRow WHERE EntryId IN ({placeholders}) GROUP BY EntryId",
                entryIds.Cast<object>().ToArray())
            .ToDictionary(r => r.EntryId, r => r.Count);
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

// IsCommon is the entry's flag; FormIsCommon is whether the matched spelling or
// reading itself is common (大雪 is common as おおゆき, not as たいせつ).
public sealed record ExactTextMatch(string Text, int EntryId, IReadOnlyList<WordClass> WordClasses, bool IsCommon, string PrimaryText, bool FormIsCommon);
