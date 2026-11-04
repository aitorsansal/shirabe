using JapaneseDictionary.Core.Export;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Tests;

public class WordListCsvTests
{
    private const string Headers =
        "#separator:Comma\r\n#html:true\r\n#columns:Word,Back,Reading,Meanings,Tags,GUID\r\n#tags column:5\r\n#guid column:6\r\n";

    private static DictionaryEntry Entry(string? kanji, string reading, int id, params string[][] senses) => new(
        id,
        kanji == null ? [] : [new KanjiForm(kanji, true)],
        [new ReadingForm(reading, true, true, [])],
        senses.Select(g => new WordSense([], [], g, [], [])).ToList());

    private static WordListCsv.Row R(DictionaryEntry entry, int? jlpt = null, params TranslatedSense[] translations) =>
        new(entry, jlpt, translations);

    private static string Body(string csv)
    {
        Assert.StartsWith(Headers, csv);
        return csv[Headers.Length..];
    }

    [Fact]
    public void Build_WritesAnkiHeaderLines()
    {
        Assert.Equal(Headers, WordListCsv.Build([], "Favourites"));
    }

    [Fact]
    public void Build_SingleSense_HasNoNumbering()
    {
        var csv = WordListCsv.Build([R(Entry("猫", "ねこ", 5, ["cat"]))], "Animals");
        Assert.Equal("猫,ねこ<br>cat,ねこ,cat,shirabe Animals,shirabe-5\r\n", Body(csv));
    }

    [Fact]
    public void Build_MultipleSenses_NumbersThemAndJoinsGlosses()
    {
        var csv = WordListCsv.Build([R(Entry("食べる", "たべる", 1358280, ["to eat"], ["to live on", "to live off"]))], "L");
        Assert.Equal(
            "食べる,\"たべる<br>1. to eat<br>2. to live on, to live off\",たべる,\"to eat; to live on, to live off\",shirabe L,shirabe-1358280\r\n",
            Body(csv));
    }

    [Fact]
    public void Build_SkipsSensesWithoutGlosses()
    {
        var csv = WordListCsv.Build([R(Entry("猫", "ねこ", 5, ["cat"], []))], "L");
        Assert.Equal("猫,ねこ<br>cat,ねこ,cat,shirabe L,shirabe-5\r\n", Body(csv));
    }

    [Fact]
    public void Build_AddsLineWithEndonymPerTranslationLanguage()
    {
        var entry = Entry("食べる", "たべる", 1, ["to eat"]);
        var csv = WordListCsv.Build(
        [
            R(entry, null,
                new TranslatedSense("spa", 2, ["vivir de"]),
                new TranslatedSense("spa", 1, ["comer", "ingerir"]),
                new TranslatedSense("ger", 1, ["essen"])),
        ], "L");

        Assert.Equal(
            "食べる,\"たべる<br>to eat<br>Español: comer, ingerir; vivir de<br>Deutsch: essen\",たべる,to eat,shirabe L,shirabe-1\r\n",
            Body(csv));
    }

    [Fact]
    public void Build_AddsJlptTagOnlyWhenLevelKnown()
    {
        var entry = Entry("猫", "ねこ", 5, ["cat"]);
        Assert.EndsWith(",shirabe L JLPT_N5,shirabe-5\r\n", WordListCsv.Build([R(entry, 5)], "L"));
        Assert.EndsWith(",shirabe L,shirabe-5\r\n", WordListCsv.Build([R(entry)], "L"));
    }

    [Fact]
    public void Build_ReplacesWhitespaceInListNameWithUnderscores()
    {
        var entry = Entry("猫", "ねこ", 5, ["cat"]);
        Assert.EndsWith(",shirabe My_best_words,shirabe-5\r\n",WordListCsv.Build([R(entry)], "  My best \t words "));
    }

    [Fact]
    public void Build_OmitsListTagWhenNameIsBlank()
    {
        var entry = Entry("猫", "ねこ", 5, ["cat"]);
        Assert.EndsWith(",shirabe,shirabe-5\r\n", WordListCsv.Build([R(entry)], "  "));
    }

    [Fact]
    public void Build_EscapesHtmlInAllTextFields()
    {
        var csv = WordListCsv.Build([R(Entry("猫", "ねこ", 5, ["a <b> & c"]))], "L");
        Assert.Equal("猫,ねこ<br>a &lt;b&gt; &amp; c,ねこ,a &lt;b&gt; &amp; c,shirabe L,shirabe-5\r\n", Body(csv));
    }

    [Fact]
    public void Build_QuotesFieldsWithCommasAndDoublesQuotes()
    {
        var csv = WordListCsv.Build([R(Entry("猫", "ねこ", 5, ["say \"hi\", loudly"]))], "L");
        Assert.Equal(
            "猫,\"ねこ<br>say \"\"hi\"\", loudly\",ねこ,\"say \"\"hi\"\", loudly\",shirabe L,shirabe-5\r\n",
            Body(csv));
    }

    [Fact]
    public void Build_WritesStableGuidFromEntryId()
    {
        var csv = WordListCsv.Build([R(Entry(null, "すごい", 1234, ["amazing"]))], "L");
        Assert.EndsWith(",shirabe-1234\r\n", csv);
        Assert.StartsWith("すごい,", Body(csv));
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
    public void ToBytes_HasNoBom_SoTheFirstAnkiDirectiveStartsTheFile()
    {
        Assert.Equal((byte)'#', WordListCsv.ToBytes("#separator:Comma\r\n")[0]);
    }
}
