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

    [SkippableTheory]
    [InlineData("た?る")]
    [InlineData("食*")]
    [InlineData("𠮟*")]
    [InlineData("*べる")]
    [InlineData("tabe*")]
    [InlineData("kon*")]
    [InlineData("ta?eru")]
    public void Search_Wildcards_OnlyReturnMatchingJapaneseForms(string query)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search(query);
        Assert.NotEmpty(results);

        // Romaji runs as a hiragana and a katakana pattern; a result may match either.
        var patterns = (query.Any(char.IsAsciiLetter)
                ? new[] { RomajiConverter.ToHiragana(query), RomajiConverter.ToKatakana(query) }
                : [query])
            .Select(p => "^" + System.Text.RegularExpressions.Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$")
            .ToList();
        Assert.All(results, result =>
        {
            Assert.Equal(SearchMatchKind.Wildcard, result.Kind);
            var entry = repo!.GetEntry(result.EntryId)!;
            Assert.Contains(entry.KanjiForms.Select(f => f.Text).Concat(entry.Readings.Select(f => f.Text)),
                text => patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(text, p)));
        });
    }

    [SkippableFact]
    public void Search_RomajiWildcard_RanksHiraganaAndKatakanaTogether()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        // び* alone has well over 20 matches; a common katakana word must still make
        // the first 20 instead of queueing behind every hiragana one.
        var top = search!.Search("bi*", 20).Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();
        Assert.Contains(top, text => text is "ビル" or "ビール" or "ビデオ");
    }

    [SkippableFact]
    public void Search_Wildcards_FindExpectedWordsAndRankCommonFormsFirst()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var food = search!.Search("食*").Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();
        Assert.Contains("食べる", food);
        Assert.Contains("食事", food);
        var suffix = search.Search("*べる");
        Assert.Contains(suffix, r => repo!.GetEntry(r.EntryId)!.PrimaryText == "食べる");
        Assert.Contains(suffix, r => repo!.GetEntry(r.EntryId)!.PrimaryText == "調べる");
        Assert.Equal(suffix, search.Search("＊べる"));
        var romaji = search.Search("tabe*");
        Assert.Contains(romaji, r => r.EntryId == 1358280);
        Assert.Contains(romaji, r => repo!.GetEntry(r.EntryId)!.PrimaryText == "食べ物");
        Assert.Equal(1358280, search.Search("食べ*")[0].EntryId);
        Assert.Empty(search.Search("*"));
        Assert.Empty(search.Search("？"));
        Assert.Empty(search.Search("*?＊？"));
        Assert.DoesNotContain(search.Search("eat*"), r => r.Kind == SearchMatchKind.Wildcard);
        Assert.Empty(search.Search("た[?る"));
    }

    [SkippableFact]
    public void Search_ExactKanji_FindsTaberu()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("食べる");
        Assert.Contains(results, r => r.EntryId == 1358280 && r.Kind == SearchMatchKind.ExactMatch);
    }

    [SkippableFact]
    public void Search_SingleKanji_AlsoFindsCompoundsContainingIt()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("行");

        // Hundreds of 行 words start with it; the compounds must still get room.
        var compounds = results.Where(r => r.Kind == SearchMatchKind.Contains).ToList();
        Assert.NotEmpty(compounds);
        Assert.All(compounds, r => Assert.Contains("行", repo!.GetEntry(r.EntryId)!.PrimaryText));
        Assert.Contains(results, r => r.Kind == SearchMatchKind.Contains && repo!.GetEntry(r.EntryId)!.PrimaryText == "銀行");
        Assert.True(results.Count <= 50);
        Assert.Equal(results.Count, results.Select(r => r.EntryId).Distinct().Count());
    }

    [SkippableFact]
    public void Search_LongerKanjiQuery_DoesNotSearchForContainingWords()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        Assert.DoesNotContain(search!.Search("食堂車"), r => r.Kind == SearchMatchKind.Contains);
        Assert.DoesNotContain(search.Search("食べる"), r => r.Kind == SearchMatchKind.Contains);
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

    [SkippableTheory]
    [InlineData("食べさせます", 1358280)]
    [InlineData("たべさせられます", 1358280)]
    public void Search_PoliteCausative_FindsTaberu(string query, int entryId)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var hit = search!.Search(query).FirstOrDefault(r => r.EntryId == entryId);
        Assert.NotNull(hit);
        Assert.Equal(SearchMatchKind.Conjugated, hit.Kind);
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
        var results = search!.Search("to eat");
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
    public void Search_Homophones_RankByFrequency()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var heads = search!.Search("kami").Where(r => r.Kind == SearchMatchKind.ExactMatch)
            .Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();

        // 紙, 神 and 髪 (nf03–nf06) lead, 加味 (nf15) follows, and 上 read as かみ
        // (ichi1 only) comes after all of them.
        Assert.Equal(new HashSet<string> { "紙", "神", "髪" }, heads.Take(3).ToHashSet());
        Assert.True(heads.IndexOf("加味") < heads.IndexOf("上"));
    }

    [SkippableTheory]
    [InlineData("nagara", "ながら")]
    [InlineData("shimatta", "しまった")]
    [InlineData("arigatou", "ありがとう")]
    public void Search_UsuallyKanaWords_LeadWithTheReading(string query, string expectedHeadword)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var first = search!.Search(query)[0];
        Assert.Equal(expectedHeadword, repo!.GetEntry(first.EntryId)!.PrimaryText);
    }

    [SkippableFact]
    public void Search_NegativeConditionalOfIku_FindsOnlyTheVerbs()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var heads = search!.Search("ikanakereba").Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();
        Assert.Contains("行く", heads);
        Assert.DoesNotContain("いい", heads);
        Assert.DoesNotContain("良い", heads);
    }

    [SkippableFact]
    public void Search_ConjugatedKana_FindsKikuAsPolite()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("ききます");
        Assert.Contains(results, r => r.Kind == SearchMatchKind.Conjugated && r.ConjugationForm == "polite");
    }

    private List<string> Heads(string query) =>
        search!.Search(query).Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();

    [SkippableTheory]
    [InlineData("eat", "食べる")]
    [InlineData("write", "書く")]
    [InlineData("sit", "座る")]
    [InlineData("understand", "分かる")]
    [InlineData("きく", "聞く")]
    [InlineData("する", "する")]
    public void Search_BasicWord_RanksFirst(string query, string expected)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        Assert.Equal(expected, Heads(query)[0]);
    }

    [SkippableTheory]
    [InlineData("go", "行く", 3)]
    [InlineData("do", "する", 2)]
    [InlineData("leg", "足", 2)]
    [InlineData("wake up", "起きる", 3)]
    [InlineData("arrive", "着く", 3)]
    [InlineData("search", "探す", 4)]
    [InlineData("drink", "飲む", 2)]
    [InlineData("enter", "入る", 2)]
    [InlineData("かく", "書く", 2)]
    [InlineData("おく", "置く", 2)]
    public void Search_BasicWord_RanksInTopN(string query, string expected, int n)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        Assert.Contains(expected, Heads(query).Take(n));
    }

    [SkippableFact]
    public void Search_Suru_RanksTheVerbWithManySensesFirst()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var first = repo!.GetEntry(search!.Search("する")[0].EntryId)!;
        Assert.True(first.Senses.Count > 5);
    }
}
