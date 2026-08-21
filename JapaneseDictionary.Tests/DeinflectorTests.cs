using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class DeinflectorTests
{
    [Theory]
    [InlineData("たべた", "たべる", WordClass.IchidanVerb, "past")]
    [InlineData("たべない", "たべる", WordClass.IchidanVerb, "negative")]
    [InlineData("たべます", "たべる", WordClass.IchidanVerb, "polite")]
    [InlineData("たべて", "たべる", WordClass.IchidanVerb, "te-form")]
    public void Ichidan_MatchesKnownForms(string query, string dictionaryReading, WordClass wordClass, string expectedForm)
    {
        Assert.Equal(expectedForm, Deinflector.TryGetFormName(query, dictionaryReading, wordClass));
    }

    [Theory]
    [InlineData("かいた", "かく", WordClass.GodanVerbKu, "past")] // 書いた: irregular い euphonic change, not 書きた
    [InlineData("かかない", "かく", WordClass.GodanVerbKu, "negative")]
    [InlineData("かきます", "かく", WordClass.GodanVerbKu, "polite")]
    [InlineData("およいで", "およぐ", WordClass.GodanVerbGu, "te-form")] // 泳いで: voiced euphonic change
    [InlineData("はなして", "はなす", WordClass.GodanVerbSu, "te-form")] // 話して: no euphonic change for su-row
    [InlineData("まって", "まつ", WordClass.GodanVerbTsu, "te-form")]
    [InlineData("しんだ", "しぬ", WordClass.GodanVerbNu, "past")]
    [InlineData("あそんだ", "あそぶ", WordClass.GodanVerbBu, "past")]
    [InlineData("よんだ", "よむ", WordClass.GodanVerbMu, "past")]
    [InlineData("かえって", "かえる", WordClass.GodanVerbRu, "te-form")]
    [InlineData("かった", "かう", WordClass.GodanVerbU, "past")]
    [InlineData("いって", "いく", WordClass.GodanVerbIkuYuku, "te-form")] // irregular: NOT いいて
    public void Godan_MatchesKnownForms(string query, string dictionaryReading, WordClass wordClass, string expectedForm)
    {
        Assert.Equal(expectedForm, Deinflector.TryGetFormName(query, dictionaryReading, wordClass));
    }

    [Theory]
    [InlineData("たかくない", "たかい", "negative")]
    [InlineData("たかかった", "たかい", "past")]
    [InlineData("たかくて", "たかい", "te-form")]
    public void IAdjective_MatchesKnownForms(string query, string dictionaryReading, string expectedForm)
    {
        Assert.Equal(expectedForm, Deinflector.TryGetFormName(query, dictionaryReading, WordClass.IAdjective));
    }

    [Theory]
    [InlineData("べんきょうしない", "べんきょうする", "negative")]
    [InlineData("べんきょうした", "べんきょうする", "past")]
    [InlineData("べんきょうして", "べんきょうする", "te-form")]
    public void Suru_MatchesKnownForms(string query, string dictionaryReading, string expectedForm)
    {
        Assert.Equal(expectedForm, Deinflector.TryGetFormName(query, dictionaryReading, WordClass.SuruVerb));
    }

    [Theory]
    [InlineData("こない", "negative")]
    [InlineData("きた", "past")]
    [InlineData("きて", "te-form")]
    public void Kuru_MatchesKnownForms(string query, string expectedForm)
    {
        Assert.Equal(expectedForm, Deinflector.TryGetFormName(query, "くる", WordClass.KuruVerb));
    }

    [Fact]
    public void UnrelatedQuery_DoesNotMatch()
    {
        Assert.Null(Deinflector.TryGetFormName("ねる", "たべる", WordClass.IchidanVerb));
    }
}

public class ConjugationTableTests
{
    [Fact]
    public void Ichidan_TableHasNonPastAndPastRows()
    {
        var rows = Deinflector.GetConjugationTable("たべる", WordClass.IchidanVerb);
        var nonPast = Assert.Single(rows, r => r.Label == "Non-past");
        Assert.Equal("たべる", nonPast.Plain);
        Assert.Equal("たべます", nonPast.Polite);

        var past = Assert.Single(rows, r => r.Label == "Past");
        Assert.Equal("たべた", past.Plain);
        Assert.Equal("たべました", past.Polite);
    }

    [Fact]
    public void Ichidan_PotentialAndPassiveShareSameSurfaceForm()
    {
        var rows = Deinflector.GetConjugationTable("たべる", WordClass.IchidanVerb);
        var potential = Assert.Single(rows, r => r.Label == "Potential");
        var passive = Assert.Single(rows, r => r.Label == "Passive");
        Assert.Equal("たべられる", potential.Plain);
        Assert.Equal(potential.Plain, passive.Plain);
    }

    [Fact]
    public void Godan_ImperativeIsEStemAlone()
    {
        var rows = Deinflector.GetConjugationTable("かく", WordClass.GodanVerbKu);
        var imperative = Assert.Single(rows, r => r.Label == "Imperative");
        Assert.Equal("かけ", imperative.Plain);
    }

    [Fact]
    public void IAdjective_HasNoImperativeRow()
    {
        var rows = Deinflector.GetConjugationTable("たかい", WordClass.IAdjective);
        Assert.DoesNotContain(rows, r => r.Label == "Imperative");
    }
}
