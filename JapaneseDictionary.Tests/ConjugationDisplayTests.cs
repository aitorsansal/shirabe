using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class ConjugationDisplayTests
{
    private static Deinflector.ConjugationDisplayRow[] Table(string headword, string reading, WordClass wc, params string[] tags) =>
        Deinflector.GetConjugationTable(headword, reading, wc, Deinflector.VariantFromTags(tags)).ToArray();

    private static string Plain(Deinflector.ConjugationDisplayRow[] rows, string label) =>
        Assert.Single(rows, r => r.Label == label).Plain?.Display ?? "—";

    private static string Polite(Deinflector.ConjugationDisplayRow[] rows, string label) =>
        Assert.Single(rows, r => r.Label == label).Polite?.Display ?? "—";

    [Fact]
    public void Ichidan_ShowsKanjiAndReading()
    {
        var rows = Table("食べる", "たべる", WordClass.IchidanVerb, "v1");
        Assert.Equal("食べる", Plain(rows, "Non-past"));
        Assert.Equal("食べます", Polite(rows, "Non-past"));
        Assert.Equal("食べなかった", Plain(rows, "Past neg."));
        var cell = Assert.Single(rows, r => r.Label == "Non-past").Polite!;
        Assert.Equal("たべます", cell.Reading);
        Assert.True(cell.HasReading);
    }

    [Fact]
    public void KanaHeadword_HasNoSeparateReading()
    {
        var rows = Table("しまう", "しまう", WordClass.GodanVerbU, "v5u");
        var cell = Assert.Single(rows, r => r.Label == "Past").Plain!;
        Assert.Equal("しまった", cell.Display);
        Assert.False(cell.HasReading);
    }

    [Fact]
    public void Kuru_KanjiCoversChangingKana()
    {
        var rows = Table("来る", "くる", WordClass.KuruVerb, "vk");
        Assert.Equal("来る", Plain(rows, "Non-past"));
        Assert.Equal("来ない", Plain(rows, "Negative"));
        Assert.Equal("来ます", Polite(rows, "Non-past"));
        Assert.Equal("来た", Plain(rows, "Past"));
        Assert.Equal("来られる", Plain(rows, "Potential"));
        Assert.Equal("来い", Plain(rows, "Imperative"));
    }

    [Fact]
    public void KuruCompound_KeepsPrefix()
    {
        var rows = Table("持って来る", "もってくる", WordClass.KuruVerb, "vk");
        Assert.Equal("持って来ない", Plain(rows, "Negative"));
        Assert.Equal("持って来ました", Polite(rows, "Past"));
    }

    [Fact]
    public void SuruNoun_AttachesSuru()
    {
        var rows = Table("勉強", "べんきょう", WordClass.SuruVerb, "vs", "n");
        Assert.Equal("勉強する", Plain(rows, "Non-past"));
        Assert.Equal("勉強します", Polite(rows, "Non-past"));
        Assert.Equal("勉強した", Plain(rows, "Past"));
    }

    [Fact]
    public void Godan_Iku()
    {
        var rows = Table("行く", "いく", WordClass.GodanVerbIkuYuku, "v5k-s");
        Assert.Equal("行って", Plain(rows, "Te-form"));
        Assert.Equal("行った", Plain(rows, "Past"));
        Assert.Equal("行きたい", Plain(rows, "Want to"));
    }

    [Fact]
    public void AdjIx_FallsBackToKanaWhenReadingPrefixDiffers()
    {
        var rows = Table("良い", "いい", WordClass.IAdjective, "adj-ix");
        Assert.Equal("よくない", Plain(rows, "Negative"));
        Assert.Equal("よかった", Plain(rows, "Past"));
        Assert.Equal("良いです", Polite(rows, "Non-past"));
    }

    [Fact]
    public void AdjIx_CompoundKeepsKanjiPrefix()
    {
        var rows = Table("格好いい", "かっこいい", WordClass.IAdjective, "adj-ix");
        Assert.Equal("格好よくない", Plain(rows, "Negative"));
    }

    [Fact]
    public void Aru_NegativeIsNai_NoPotential()
    {
        var rows = Table("有る", "ある", WordClass.GodanVerbRu, "v5r-i");
        Assert.Equal("ない", Plain(rows, "Negative"));
        Assert.Equal("なかった", Plain(rows, "Past neg."));
        Assert.DoesNotContain(rows, r => r.Label == "Potential");
        Assert.Equal("有ります", Polite(rows, "Non-past"));
        Assert.Equal("有った", Plain(rows, "Past"));
        Assert.Equal("有れば", Plain(rows, "Conditional (ba)"));
        Assert.Equal("有ろう", Plain(rows, "Volitional"));
        Assert.Equal("有れ", Plain(rows, "Imperative"));
    }

    [Fact]
    public void V5aru_PoliteStemIsI()
    {
        var rows = Table("下さる", "くださる", WordClass.GodanVerbRu, "v5aru");
        Assert.Equal("下さいます", Polite(rows, "Non-past"));
        Assert.Equal("下さいません", Polite(rows, "Negative"));
        Assert.Equal("下さいました", Polite(rows, "Past"));
        Assert.Equal("下さいませんでした", Polite(rows, "Past neg."));
        Assert.Equal("下さいまして", Polite(rows, "Te-form"));
        Assert.Equal("下さいましょう", Polite(rows, "Volitional"));
        Assert.Equal("下さい", Plain(rows, "Imperative"));
        Assert.Equal("下さらない", Plain(rows, "Negative"));
        Assert.Equal("下さった", Plain(rows, "Past"));
        Assert.Equal("下されば", Plain(rows, "Conditional (ba)"));
    }

    [Theory]
    [InlineData("くださる", "ください")]
    [InlineData("いらっしゃる", "いらっしゃい")]
    [InlineData("なさる", "なさい")]
    public void V5aru_ImperativeIsRootPlusI(string reading, string imperative)
    {
        var rows = Table(reading, reading, WordClass.GodanVerbRu, "v5aru");
        Assert.Equal(imperative, Plain(rows, "Imperative"));
    }

    [Fact]
    public void V5uS_KeepsUInTeAndTa()
    {
        var rows = Table("問う", "とう", WordClass.GodanVerbU, "v5u-s");
        Assert.Equal("問うて", Plain(rows, "Te-form"));
        Assert.Equal("問うた", Plain(rows, "Past"));
        Assert.Equal("問うたら", Plain(rows, "Conditional (tara)"));
        Assert.Equal("問わない", Plain(rows, "Negative"));
    }

    [Fact]
    public void VsS_UsesSaRowStems()
    {
        var rows = Table("愛する", "あいする", WordClass.SuruVerb, "vs-s");
        Assert.Equal("愛する", Plain(rows, "Non-past"));
        Assert.Equal("愛さない", Plain(rows, "Negative"));
        Assert.Equal("愛さなかった", Plain(rows, "Past neg."));
        Assert.Equal("愛される", Plain(rows, "Passive"));
        Assert.Equal("愛させる", Plain(rows, "Causative"));
        Assert.Equal("愛せる", Plain(rows, "Potential"));
        Assert.Equal("愛そう", Plain(rows, "Volitional"));
        Assert.Equal("愛せ", Plain(rows, "Imperative"));
        Assert.Equal("愛します", Polite(rows, "Non-past"));
        Assert.Equal("愛して", Plain(rows, "Te-form"));
        Assert.Equal("愛した", Plain(rows, "Past"));
        Assert.Equal("愛すれば", Plain(rows, "Conditional (ba)"));
    }

    [Fact]
    public void Vz_ConjugatesLikeJiru()
    {
        var rows = Table("信ずる", "しんずる", WordClass.IchidanVerb, "vz");
        Assert.Equal("信ずる", Plain(rows, "Non-past"));
        Assert.Equal("信じない", Plain(rows, "Negative"));
        Assert.Equal("信じます", Polite(rows, "Non-past"));
        Assert.Equal("信じた", Plain(rows, "Past"));
        Assert.Equal("信じて", Plain(rows, "Te-form"));
        Assert.Equal("信じれば", Plain(rows, "Conditional (ba)"));
        Assert.Equal("信じられる", Plain(rows, "Potential"));
        Assert.Equal("信じさせる", Plain(rows, "Causative"));
        Assert.Equal("信じよう", Plain(rows, "Volitional"));
        Assert.Equal("信じろ", Plain(rows, "Imperative"));
    }

    [Fact]
    public void V1s_Kureru_ImperativeIsKure()
    {
        var rows = Table("呉れる", "くれる", WordClass.IchidanVerb, "v1-s");
        Assert.Equal("呉れ", Plain(rows, "Imperative"));
    }

    [Fact]
    public void VariantFor_UsesSensesCarryingThePrimaryClass()
    {
        var entry = new DictionaryEntry(1, [], [new ReadingForm("ある", true, true, [])],
        [
            new WordSense([WordClass.Other], ["n"], ["x"], [], []),
            new WordSense([WordClass.GodanVerbRu], ["v5r-i", "vi"], ["to exist"], [], []),
        ]);
        Assert.Equal(ConjugationVariant.GodanAruIrregular, Deinflector.VariantFor(entry, WordClass.GodanVerbRu));
        Assert.Equal(ConjugationVariant.None, Deinflector.VariantFor(entry, WordClass.IchidanVerb));
    }

    [Fact]
    public void VariantFor_WorksOnEntriesLoadedFromTheDatabase()
    {
        // DictionaryRepository.GetEntry leaves every sense's WordClasses empty;
        // only the raw tags survive, so the variant has to come from them.
        var entry = new DictionaryEntry(1, [new KanjiForm("有る", true)], [new ReadingForm("ある", true, true, [])],
        [
            new WordSense([], ["v5r-i", "vi"], ["to exist"], [], ["uk"]),
        ]);
        Assert.Equal(ConjugationVariant.GodanAruIrregular, Deinflector.VariantFor(entry, WordClass.GodanVerbRu));
    }

    [Fact]
    public void RegularSearchForms_AreNotAffectedByVariants()
    {
        Assert.Equal("negative", Deinflector.TryGetFormName("あらない", "ある", WordClass.GodanVerbRu));
    }

    [Fact]
    public void Ichidan_HasAllRowsInOrder()
    {
        var rows = Table("食べる", "たべる", WordClass.IchidanVerb, "v1");
        Assert.Equal(
            ["Non-past", "Past", "Negative", "Past neg.", "Te-form", "Negative te-form", "Potential", "Passive",
             "Causative", "Causative passive", "Volitional", "Want to", "Conditional (ba)", "Conditional (tara)", "Imperative"],
            rows.Select(r => r.Label));
        Assert.Equal("食べて", Plain(rows, "Te-form"));
        Assert.Equal("食べまして", Polite(rows, "Te-form"));
        Assert.Equal("食べないで", Plain(rows, "Negative te-form"));
        Assert.Equal("—", Polite(rows, "Negative te-form"));
        Assert.Equal("食べられる", Plain(rows, "Potential"));
        Assert.Equal("食べられます", Polite(rows, "Potential"));
        Assert.Equal("食べさせられる", Plain(rows, "Causative passive"));
        Assert.Equal("食べよう", Plain(rows, "Volitional"));
        Assert.Equal("食べましょう", Polite(rows, "Volitional"));
        Assert.Equal("食べたい", Plain(rows, "Want to"));
        Assert.Equal("食べたいです", Polite(rows, "Want to"));
        Assert.Equal("食べれば", Plain(rows, "Conditional (ba)"));
        Assert.Equal("食べたら", Plain(rows, "Conditional (tara)"));
        Assert.Equal("食べましたら", Polite(rows, "Conditional (tara)"));
        Assert.Equal("食べろ", Plain(rows, "Imperative"));
    }

    [Fact]
    public void Godan_NewRows()
    {
        var rows = Table("書く", "かく", WordClass.GodanVerbKu, "v5k");
        Assert.Equal("書いて", Plain(rows, "Te-form"));
        Assert.Equal("書きまして", Polite(rows, "Te-form"));
        Assert.Equal("書かないで", Plain(rows, "Negative te-form"));
        Assert.Equal("書ける", Plain(rows, "Potential"));
        Assert.Equal("書けます", Polite(rows, "Potential"));
        Assert.Equal("書かせられる", Plain(rows, "Causative passive"));
        Assert.Equal("書こう", Plain(rows, "Volitional"));
        Assert.Equal("書きましょう", Polite(rows, "Volitional"));
        Assert.Equal("書きたいです", Polite(rows, "Want to"));
        Assert.Equal("書いたら", Plain(rows, "Conditional (tara)"));
        Assert.Equal("書きましたら", Polite(rows, "Conditional (tara)"));
    }

    [Fact]
    public void IAdjective_NewRows()
    {
        var rows = Table("高い", "たかい", WordClass.IAdjective, "adj-i");
        Assert.Equal("高くなくて", Plain(rows, "Negative te-form"));
        Assert.Equal("高かったら", Plain(rows, "Conditional (tara)"));
        Assert.Equal("高くて", Plain(rows, "Te-form"));
        Assert.DoesNotContain(rows, r => r.Label is "Want to" or "Potential" or "Imperative" or "Volitional");
    }

    [Fact]
    public void NaAdjective_NewRows()
    {
        var rows = Table("静か", "しずか", WordClass.NaAdjective, "adj-na");
        Assert.Equal("静かだ", Plain(rows, "Non-past"));
        Assert.Equal("静かです", Polite(rows, "Non-past"));
        Assert.Equal("静かだったら", Plain(rows, "Conditional (tara)"));
        Assert.DoesNotContain(rows, r => r.Label is "Want to" or "Potential" or "Imperative");
    }
}
