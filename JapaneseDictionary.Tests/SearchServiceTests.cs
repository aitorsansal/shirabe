using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Search;

using Xunit;

namespace JapaneseDictionary.Tests;

public class SearchServiceTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private readonly DictionaryRepository? repo;
    private readonly SearchService? search;

    public SearchServiceTests()
    {
        if (File.Exists(DbPath))
        {
            repo = new DictionaryRepository(DbPath);
            search = new SearchService(repo);
        }
    }

    public void Dispose() => repo?.Dispose();

    [SkippableFact]
    public void Search_ExactKanji_FindsTaberu()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("食べる");
        Assert.Contains(results, r => r.EntryId == 1358280 && r.Kind == SearchMatchKind.ExactMatch);
    }

    [SkippableFact]
    public void Search_ConjugatedKana_FindsTaberuAsPast()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("たべた");
        var hit = results.SingleOrDefault(r => r.EntryId == 1358280);
        Assert.NotNull(hit);
        Assert.Equal(SearchMatchKind.Conjugated, hit.Kind);
        Assert.Equal("past", hit.ConjugationForm);
    }

    [SkippableFact]
    public void Search_Romaji_FindsTaberu()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("taberu");
        Assert.Contains(results, r => r.EntryId == 1358280);
    }

    [SkippableFact]
    public void Search_EnglishGloss_FindsToEat()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("to eat", SearchMode.English);
        Assert.Contains(results, r => r.EntryId == 1358280 && r.Kind == SearchMatchKind.Gloss);
    }

    [SkippableFact]
    public void Search_KanaPrefix_FindsTaberu()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("たべ");
        Assert.Contains(results, r => r.EntryId == 1358280);
    }

    [SkippableFact]
    public void Search_ConjugatedKana_FindsKikuAsPolite()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("ききます");
        Assert.Contains(results, r => r.Kind == SearchMatchKind.Conjugated && r.ConjugationForm == "polite");
    }
}
