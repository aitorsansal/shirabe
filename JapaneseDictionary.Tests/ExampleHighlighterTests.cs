using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class ExampleHighlighterTests
{
    private static DictionaryEntry Entry(string? kanji, string reading) => new(
        1,
        kanji == null ? [] : [new KanjiForm(kanji, true)],
        [new ReadingForm(reading, true, true, [])],
        [new WordSense([], [], ["x"], [], [])]);

    private static string? Highlight(string sentence, DictionaryEntry entry, params WordClass[] classes) =>
        ExampleHighlighter.Find(sentence, ExampleHighlighter.Candidates(entry, classes)) is { } hit
            ? sentence.Substring(hit.Index, hit.Length)
            : null;

    [Theory]
    [InlineData("きょう何を昼食に食べましたか。", "食べました")]
    [InlineData("いつから日本人は精白米を食べるようになったのですか？", "食べる")]
    [InlineData("もう食べている。", "食べて")]
    [InlineData("たべたい", "たべたい")]
    public void Find_MatchesDictionaryAndConjugatedForms_Longest(string sentence, string expected)
    {
        Assert.Equal(expected, Highlight(sentence, Entry("食べる", "たべる"), WordClass.IchidanVerb));
    }

    [Fact]
    public void Find_EqualLengthMatches_PicksEarliest()
    {
        var entry = new DictionaryEntry(1, [new KanjiForm("行く", true)], [new ReadingForm("いく", true, true, []), new ReadingForm("ゆく", false, true, [])], [new WordSense([], [], ["x"], [], [])]);
        Assert.Equal("行く", Highlight("あなたの行くところなら、どこへでも付いてゆく", entry, WordClass.GodanVerbIkuYuku));
    }

    [Fact]
    public void Find_GodanTeForm()
    {
        Assert.Equal("飲んで", Highlight("水を飲んでください。", Entry("飲む", "のむ"), WordClass.GodanVerbMu));
    }

    [Fact]
    public void Find_SuruNoun()
    {
        // The conjugation tables stop at the te-form, so -ています highlights as 勉強して.
        Assert.Equal("勉強して", Highlight("今、日本語を勉強しています。", Entry("勉強", "べんきょう"), WordClass.SuruVerb));
    }

    [Fact]
    public void Candidates_DropShortKanaOnlyForms()
    {
        // いる's te-form いて would otherwise match inside unrelated words.
        var candidates = ExampleHighlighter.Candidates(Entry(null, "いる"), [WordClass.IchidanVerb]);
        Assert.DoesNotContain("いて", candidates);
        Assert.Contains("いる", candidates);
        Assert.Contains("いました", candidates);
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        Assert.Null(Highlight("猫がいる。", Entry("食べる", "たべる"), WordClass.IchidanVerb));
    }
}
