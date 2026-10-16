using JapaneseDictionary.Core.Export;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Tests;

public class WordListCsvTests
{
    private static DictionaryEntry Entry(string? kanji, string reading, params string[][] senses) => new(
        1,
        kanji == null ? [] : [new KanjiForm(kanji, true)],
        [new ReadingForm(reading, true, true, [])],
        senses.Select(g => new WordSense([], [], g, [], [])).ToList());

    [Fact]
    public void Build_WritesHeaderAndOneRowPerEntry()
    {
        var csv = WordListCsv.Build([Entry("猫", "ねこ", ["cat"]), Entry(null, "すごい", ["amazing"])]);
        Assert.Equal("Word,Reading,Meanings\r\n猫,ねこ,cat\r\nすごい,すごい,amazing\r\n", csv);
    }

    [Fact]
    public void Build_JoinsSensesWithSemicolonAndGlossesWithComma_QuotingTheField()
    {
        var csv = WordListCsv.Build([Entry("食べる", "たべる", ["to eat"], ["to live on", "to live off"])]);
        Assert.EndsWith("食べる,たべる,\"to eat; to live on, to live off\"\r\n", csv);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a, b", "\"a, b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    public void Escape_FollowsRfc4180(string field, string expected)
    {
        Assert.Equal(expected, WordListCsv.Escape(field));
    }

    [Fact]
    public void ToBytes_StartsWithUtf8Bom()
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, WordListCsv.ToBytes("x")[..3]);
    }
}
