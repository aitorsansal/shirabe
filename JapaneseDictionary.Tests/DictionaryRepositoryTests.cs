using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;

using Xunit;

namespace JapaneseDictionary.Tests;

/// <summary>Integration tests against the real bundled dictionary.db3 (built by
/// JapaneseDictionary.DataBuilder). Skipped if the file hasn't been built yet.</summary>
public class DictionaryRepositoryTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private readonly DictionaryRepository? _repo;

    public DictionaryRepositoryTests()
    {
        if (File.Exists(DbPath))
            _repo = new DictionaryRepository(DbPath);
    }

    public void Dispose() => _repo?.Dispose();

    [SkippableFact]
    public void FindByExactText_FindsTaberu()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var ids = _repo!.FindByExactText("食べる");
        Assert.Contains(1358280, ids);
    }

    [SkippableFact]
    public void GetEntry_TaberuHasExpectedGlossAndClass()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var entry = _repo!.GetEntry(1358280);
        Assert.NotNull(entry);
        Assert.Equal("食べる", entry!.PrimaryText);
        Assert.Contains(entry.Senses, s => s.Glosses.Contains("to eat"));

        var classes = _repo.GetEntryWordClasses(1358280);
        Assert.Contains(WordClass.IchidanVerb, classes);
    }

    [SkippableFact]
    public void FindByPrefix_TabeMatchesTaberu()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var ids = _repo!.FindByPrefix("たべ", 100);
        Assert.Contains(1358280, ids);
    }

    [SkippableFact]
    public void FindByGlossContains_ToEatMatchesTaberu()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var ids = _repo!.FindByGlossContains("to eat", 500);
        Assert.Contains(1358280, ids);
    }

    [SkippableFact]
    public void GetKanjiInfo_TabeKanjiHasCorrectStrokeCount()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var info = _repo!.GetKanjiInfo("食");
        Assert.NotNull(info);
        Assert.Equal(9, info!.StrokeCount);
        Assert.Contains("eat", info.Meanings);
    }

    [SkippableFact]
    public void GetKanjiStrokes_TabeKanjiHasNineStrokesInOrder()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var strokes = _repo!.GetKanjiStrokes("食");
        Assert.Equal(9, strokes.Count);
        Assert.Equal(Enumerable.Range(1, 9), strokes.Select(s => s.StrokeIndex));
    }

    [SkippableFact]
    public void FindReadingCandidates_FindsTaberuAsIchidan()
    {
        Skip.If(_repo == null, "dictionary.db3 not built");
        var candidates = _repo!.FindReadingCandidates("たべ", 1000);
        Assert.Contains(candidates, c => c.EntryId == 1358280 && c.WordClasses.Contains(WordClass.IchidanVerb));
    }
}
