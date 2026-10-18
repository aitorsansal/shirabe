using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class RomajiConverterTests
{
    [Theory]
    [InlineData("たべる", "taberu")]
    [InlineData("しんぶん", "shinbun")]
    [InlineData("がっこう", "gakkou")]
    [InlineData("まっちゃ", "matcha")]
    [InlineData("しゅっちょう", "shutchou")]
    [InlineData("きょう", "kyou")]
    [InlineData("コーヒー", "koohii")]
    [InlineData("ｺｰﾋｰ", "koohii")]
    [InlineData("こんや", "kon'ya")]
    [InlineData("しんい", "shin'i")]
    [InlineData("ファイル", "fairu")]
    [InlineData("パーティー", "paatii")]
    [InlineData("ヴァイオリン", "vaiorin")]
    [InlineData("", "")]
    public void ToRomaji_ConvertsWordReadings(string kana, string expected)
    {
        Assert.Equal(expected, RomajiConverter.ToRomaji(kana));
    }

    [Theory]
    [InlineData("tabeta", "たべた")]
    [InlineData("nihongo", "にほんご")]
    [InlineData("konnichiha", "こんにちは")]
    [InlineData("gakkou", "がっこう")] // sokuon: doubled consonant -> っ
    [InlineData("kitte", "きって")]
    [InlineData("shinbun", "しんぶん")] // n before a consonant -> ん
    [InlineData("kon'ya", "こんや")] // explicit n' terminates ん before a vowel/y
    [InlineData("kyou", "きょう")] // youon (small-y digraph)
    [InlineData("sensei", "せんせい")]
    public void ToHiragana_ConvertsKnownWords(string romaji, string expected)
    {
        Assert.Equal(expected, RomajiConverter.ToHiragana(romaji));
    }

    [Theory]
    [InlineData("sushi", "スシ")]
    [InlineData("pasokon", "パソコン")]
    public void ToKatakana_ConvertsKnownWords(string romaji, string expected)
    {
        Assert.Equal(expected, RomajiConverter.ToKatakana(romaji));
    }

    [Fact]
    public void ToHiragana_PassesThroughUnrecognizedCharacters()
    {
        Assert.Equal("たべる!", RomajiConverter.ToHiragana("taberu!"));
    }

    [Theory]
    [InlineData("okuizome", "おくいぞめ", true)]
    [InlineData("Oku-izome", "おくいぞめ", true)]
    [InlineData("terebi", "テレビ", true)]
    [InlineData("weaning ceremony", "おくいぞめ", false)]
    [InlineData("cat", "ねこ", false)]
    [InlineData("", "ねこ", false)]
    public void IsRomanizationOf_DetectsGlossesThatAreJustTheReading(string gloss, string kana, bool expected)
    {
        Assert.Equal(expected, RomajiConverter.IsRomanizationOf(gloss, kana));
    }
}
