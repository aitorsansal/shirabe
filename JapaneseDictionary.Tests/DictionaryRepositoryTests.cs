using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;

using SQLite;

using Xunit;

namespace JapaneseDictionary.Tests;

/// <summary>Integration tests against the real bundled dictionary.db3 (built by
/// JapaneseDictionary.DataBuilder). Skipped if the file hasn't been built yet.</summary>
public class DictionaryRepositoryTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private readonly DictionaryRepository? repo;

    public DictionaryRepositoryTests()
    {
        if (File.Exists(DbPath))
            repo = new DictionaryRepository(DbPath);
    }

    public void Dispose() => repo?.Dispose();

    [Fact]
    public void Wildcards_EscapeBracketsAndKeepPrefixMetacharactersLiteral()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wildcards-{Guid.NewGuid():N}.db3");
        try
        {
            using (var db = new SQLiteConnection(path))
            {
                db.CreateTable<WordEntryRow>();
                db.CreateTable<KanjiFormRow>();
                db.CreateTable<ReadingFormRow>();
                var forms = new[] { "た[べる", "たべる", "たべ*る", "たべ?る", "たべ]る" };
                for (var i = 0; i < forms.Length; i++)
                {
                    db.Insert(new WordEntryRow { Id = i + 1, PrimaryText = forms[i] });
                    db.Insert(new ReadingFormRow { EntryId = i + 1, Text = forms[i] });
                }
            }

            using var repository = new DictionaryRepository(path);
            var service = new JapaneseDictionary.Core.Search.SearchService(repository);
            var bracketMatch = Assert.Single(service.Search("た[?る"));
            Assert.Equal(1, bracketMatch.EntryId);
            Assert.Equal("た[?る", bracketMatch.MatchedText);
            Assert.Equal(5, Assert.Single(service.Search("たべ]*")).EntryId);
            Assert.Equal(new[] { 1 }, repository.FindByPrefix("た[", 50));
            Assert.Equal(new[] { 3 }, repository.FindByPrefix("たべ*", 50));
            Assert.Equal(new[] { 4 }, repository.FindByPrefix("たべ?", 50));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [SkippableFact]
    public void GetJlptEntryIds_IsOrderedCachedAndOnlyIncludesJlptEntries()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.GetJlptEntryIds();
        Assert.InRange(ids.Count, 7000, 8500);
        Assert.Same(ids, repo.GetJlptEntryIds());
        Assert.Equal(ids.OrderBy(id => id), ids);
        Assert.All(ids, id => Assert.NotNull(repo.GetJlptLevel(id)));
    }

    [SkippableTheory]
    [InlineData("KanjiFormRow", "KanjiFormRow_Text")]
    [InlineData("ReadingFormRow", "ReadingFormRow_Text")]
    public void FindByPattern_LiteralPrefixUsesTextIndex(string table, string index)
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        using var db = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadOnly);
        var plan = db.Query<QueryPlanRow>($"""
            EXPLAIN QUERY PLAN SELECT f.EntryId, f.Priority, LENGTH(f.Text) AS Length,
                (f.Text = w.PrimaryText OR f.Text = w.PrimaryReading) AS IsHeadword, w.JlptLevel
            FROM {table} f JOIN WordEntryRow w ON w.Id = f.EntryId WHERE f.Text GLOB ?
            """, "食*");
        Assert.Contains(plan, row => row.Detail.Contains(index) && row.Detail.Contains("SEARCH"));
    }

    public sealed class QueryPlanRow
    {
        public string Detail { get; set; } = "";
    }

    [SkippableFact]
    public void FindByExactText_FindsTaberu()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindByExactText("食べる");
        Assert.Contains(1358280, ids);
    }

    [SkippableTheory]
    [InlineData(1311530)] // 紙(かみ)
    [InlineData(1477950)] // 髪(かみ)
    [InlineData(1364440)] // 神(かみ)
    public void GetExampleSentences_HomographsGetLinkedThroughTheirReading(int entryId)
    {
        // These headwords belong to several entries, so their index words only link
        // once the () reading picks the right one.
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.NotEmpty(repo!.GetExampleSentences(entryId, 5));
    }

    [SkippableFact]
    public void GetEntry_TaberuHasExpectedGlossAndClass()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var entry = repo!.GetEntry(1358280);
        Assert.NotNull(entry);
        Assert.Equal("食べる", entry!.PrimaryText);
        Assert.Contains(entry.Senses, s => s.Glosses.Contains("to eat"));

        var classes = repo.GetEntryWordClasses(1358280);
        Assert.Contains(WordClass.IchidanVerb, classes);
    }

    [SkippableFact]
    public void GetEntry_EverySenseOfTaberuHasPartOfSpeech()
    {
        // The bundled JMdict happens to repeat <pos> on every sense (no empty-POS sense
        // exists in the database), so this is a regression guard; the carry-forward
        // itself is covered by CarryForwardPartOfSpeech_InheritsOnlyPartOfSpeech.
        Skip.If(repo == null, "dictionary.db3 not built");
        var entry = repo!.GetEntry(1358280);
        Assert.NotNull(entry);
        Assert.All(entry!.Senses, s => Assert.Contains("v1", s.PartOfSpeechTags));
    }

    [Fact]
    public void CarryForwardPartOfSpeech_InheritsOnlyPartOfSpeech()
    {
        var first = new WordSense([], ["v1", "vt"], ["a"], ["comp"], ["col"]);
        var second = new WordSense([], [], ["b"], [], []);
        var third = new WordSense([], ["n"], ["c"], [], []);
        var fourth = new WordSense([], [], ["d"], [], []);

        var result = DictionaryRepository.CarryForwardPartOfSpeech([first, second, third, fourth]);

        Assert.Equal(["v1", "vt"], result[1].PartOfSpeechTags);
        Assert.Empty(result[1].Fields);
        Assert.Empty(result[1].MiscTags);
        Assert.Equal(["n"], result[3].PartOfSpeechTags);
    }

    [SkippableFact]
    public void FindByPrefix_TabeMatchesTaberu()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindByPrefix("たべ", 100);
        Assert.Contains(1358280, ids);
    }

    [SkippableFact]
    public void FindGlossCandidates_ToEatMatchesTaberu()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var candidates = repo!.FindGlossCandidates("to eat");
        Assert.Contains(candidates, c => c.EntryId == 1358280);
    }

    [SkippableFact]
    public void GetKanjiInfo_TabeKanjiHasCorrectStrokeCount()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var info = repo!.GetKanjiInfo("食");
        Assert.NotNull(info);
        Assert.Equal(9, info!.StrokeCount);
        Assert.Contains("eat", info.Meanings);
    }

    [SkippableFact]
    public void GetKanjiStrokes_TabeKanjiHasNineStrokesInOrder()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var strokes = repo!.GetKanjiStrokes("食");
        Assert.Equal(9, strokes.Count);
        Assert.Equal(Enumerable.Range(1, 9), strokes.Select(s => s.StrokeIndex));
    }

    [SkippableFact]
    public void FindRelatedByKanji_MatchesHeadwordsOnly_CommonFirst()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var related = repo!.FindRelatedByKanji("食", 1358280, 8).Select(id => repo.GetEntry(id)!).ToList();

        Assert.Equal(8, related.Count);
        Assert.DoesNotContain(related, e => e.Id == 1358280);
        Assert.All(related, e => Assert.Contains("食", e.PrimaryText));
        // Common words lead; くっ付く only matched through its rare 食っ付く spelling.
        Assert.All(related, e => Assert.True(e.KanjiForms.Any(k => k.IsCommon) || e.Readings.Any(r => r.IsCommon)));
    }

    private List<WordEntryRow> LoadRows(IEnumerable<int> ids)
    {
        using var conn = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadOnly);
        return ids.Select(id => conn.Get<WordEntryRow>(id)).ToList();
    }

    private static int[] EffectiveBands(IEnumerable<WordEntryRow> rows) =>
        rows.Select(r => FormPriority.EffectiveBand(r.Priority, r.JlptLevel)).ToArray();

    [SkippableFact]
    public void FindRelatedByKanji_RanksByEffectiveBand()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindRelatedByKanji("食", 1358280, 8);
        var bands = EffectiveBands(LoadRows(ids));

        Assert.Equal(bands.Order(), bands);
        Assert.Contains(repo.FindByExactText("食事").First(), ids.Take(8));
    }

    [SkippableFact]
    public void FindCompoundsByKanji_RanksByEffectiveBand()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindCompoundsByKanji("行", 50);
        var bands = EffectiveBands(LoadRows(ids));

        Assert.Equal(bands.Order(), bands);
        Assert.Contains(repo.FindByExactText("銀行"), id => ids.Take(10).Contains(id));
    }

    [SkippableFact]
    public void FindByPrefix_TaberuLeadsTabe()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindByPrefix("たべ", 20).ToList();

        // 食べる has no nf tag but is N5, which counts because たべる is its primary
        // reading; 食べ物 (1358340) is nf-tagged and used to win on band alone.
        Assert.Equal(1358280, ids[0]);
        Assert.True(ids.IndexOf(1358340) > 0);
    }

    [SkippableTheory]
    [InlineData("たべ")]
    [InlineData("食")]
    [InlineData("ア")]
    [InlineData("𠮟")] // outside the BMP: the range's upper bound must still cover it
    public void FindByPrefix_FindsTheSameEntriesAsStartsWith(string prefix)
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        using var conn = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadOnly);
        var expected = conn.Table<KanjiFormRow>().ToList().Where(r => r.Text.StartsWith(prefix, StringComparison.Ordinal)).Select(r => r.EntryId)
            .Concat(conn.Table<ReadingFormRow>().ToList().Where(r => r.Text.StartsWith(prefix, StringComparison.Ordinal)).Select(r => r.EntryId))
            .ToHashSet();

        Assert.Equal(expected, repo!.FindByPrefix(prefix, int.MaxValue).ToHashSet());
    }

    [SkippableFact]
    public void FindByExactTexts_FindsTaberuAsIchidan()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var matches = repo!.FindByExactTexts(["食べる"]);
        Assert.Contains(matches, m => m.EntryId == 1358280 && m.WordClasses.Contains(WordClass.IchidanVerb));
    }
}
