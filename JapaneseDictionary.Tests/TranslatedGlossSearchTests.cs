using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Search;

using Xunit;

namespace JapaneseDictionary.Tests;

public class TranslatedGlossSearchTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private readonly DictionaryRepository? repo;
    private readonly SearchService? search;

    public TranslatedGlossSearchTests()
    {
        if (File.Exists(DbPath))
        {
            repo = new DictionaryRepository(DbPath);
            search = new SearchService(repo);
        }
    }

    public void Dispose() => repo?.Dispose();

    [Theory]
    [InlineData("Café", "cafe")]
    [InlineData("niño", "nino")]
    [InlineData("Mädchen", "madchen")]
    [InlineData("garçon", "garcon")]
    [InlineData("eat", "eat")]
    public void Fold_RemovesCaseAndAccents(string input, string expected) =>
        Assert.Equal(expected, TextFolding.Fold(input));

    [Theory]
    [InlineData("cafe", "%__f_%")]
    [InlineData("niño", "%____%")]
    [InlineData("Café", "%__f_%")]
    [InlineData("perro", "%p_rr_%")]
    [InlineData("100%", @"%100\%%")]
    [InlineData("a_b", @"%_\_b%")]
    [InlineData(@"a\b", @"%_\\b%")]
    public void LikePattern_WildcardsAccentableLettersAndEscapes(string query, string expected) =>
        Assert.Equal(expected, TextFolding.AccentInsensitiveLikePattern(query));

    [Theory]
    [InlineData("nino", "*[nñNÑ][iìíîïīIÌÍÎÏĪ][nñNÑ][oòóôõöōOÒÓÔÕÖŌ]*")]
    [InlineData("Haus", "*[hH][aàáâãäåāAÀÁÂÃÄÅĀ][uùúûüūUÙÚÛÜŪ][sS]*")]
    [InlineData("a*?[b]", "*[aàáâãäåāAÀÁÂÃÄÅĀ][*][?][[][bB]]*")]
    [InlineData("1 2", "*1 2*")]
    public void GlobPattern_MatchesEveryAccentAndCaseVariantExactly(string query, string expected) =>
        Assert.Equal(expected, TextFolding.AccentInsensitiveGlobPattern(query));

    [Fact]
    public void GlobPattern_WholeWord_FramesTheQueryWithNonLetters() =>
        Assert.Equal("*[^0-9A-Za-zÀ-ɏ][iìíîïīIÌÍÎÏĪ][rR][^0-9A-Za-zÀ-ɏ]*", TextFolding.AccentInsensitiveGlobPattern("ir", wholeWord: true));

    [SkippableFact]
    public void Perro_Spanish_FindsInuFirst()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        Assert.Equal("犬", PrimaryTexts("perro", "spa")[0]);
    }

    [SkippableTheory]
    [InlineData("comer", "食べる", 3)]
    [InlineData("casa", "家", 3)]
    [InlineData("cafe", "コーヒー", 5)]
    [InlineData("café", "コーヒー", 5)]
    public void Spanish_FindsExpectedWordNearTop(string query, string expected, int top)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        Assert.Contains(expected, PrimaryTexts(query, "spa").Take(top));
    }

    [SkippableFact]
    public void Rojo_Spanish_FindsAkaiOrAka()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var top = PrimaryTexts("rojo", "spa").Take(3).ToList();
        Assert.True(top.Contains("赤い") || top.Contains("赤"), string.Join(", ", top));
    }

    [SkippableTheory]
    [InlineData("manger", "fre")]
    [InlineData("essen", "ger")]
    public void FrenchAndGerman_FindTaberuNearTop(string query, string lang)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var top = PrimaryTexts(query, lang).Take(3).ToList();
        Assert.True(top.Contains("食べる"), string.Join(", ", top));
    }

    [SkippableFact]
    public void TranslatedResults_CarryKindLanguageAndOriginalGloss()
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var results = search!.Search("cafe", 50, ["spa"]);
        var hit = results.First(r => r.Kind == SearchMatchKind.TranslatedGloss && repo!.GetEntry(r.EntryId)!.PrimaryText == "コーヒー");
        Assert.Equal("spa", hit.MatchedLanguage);
        Assert.Contains("café", hit.MatchedText);
    }

    [SkippableTheory]
    [InlineData("eat")]
    [InlineData("perro")]
    [InlineData("taberu")]
    public void NullOrEmptyLanguages_BehaveLikeEnglishOnly(string query)
    {
        Skip.If(search == null, "dictionary.db3 not built");
        var baseline = search!.Search(query);
        Assert.Equal(baseline, search.Search(query, 50, null));
        Assert.Equal(baseline, search.Search(query, 50, []));
        Assert.DoesNotContain(baseline, r => r.Kind == SearchMatchKind.TranslatedGloss);
    }

    private List<string> PrimaryTexts(string query, string lang) =>
        search!.Search(query, 50, [lang]).Select(r => repo!.GetEntry(r.EntryId)!.PrimaryText).ToList();
}
