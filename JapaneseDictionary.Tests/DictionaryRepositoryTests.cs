using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;

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

    [SkippableFact]
    public void FindByExactText_FindsTaberu()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.FindByExactText("食べる");
        Assert.Contains(1358280, ids);
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

    [SkippableFact]
    public void FindByExactTexts_FindsTaberuAsIchidan()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var matches = repo!.FindByExactTexts(["食べる"]);
        Assert.Contains(matches, m => m.EntryId == 1358280 && m.WordClasses.Contains(WordClass.IchidanVerb));
    }
}
