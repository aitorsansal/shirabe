using JapaneseDictionary.Core.Models;

using Xunit;

namespace JapaneseDictionary.Tests;

public class FormPriorityTests
{
    [Theory]
    [InlineData(new[] { "ichi1", "news1", "nf03" }, 3)]
    [InlineData(new[] { "news1", "nf15" }, 15)]
    [InlineData(new[] { "ichi1" }, FormPriority.CommonUnranked)]
    [InlineData(new[] { "spec1" }, FormPriority.CommonUnranked)]
    [InlineData(new[] { "news2", "ichi2" }, FormPriority.Secondary)]
    [InlineData(new[] { "ichi1", "news2", "nf26" }, 26)]
    [InlineData(new string[0], FormPriority.None)]
    public void FromMarkers_TakesTheMostFrequentMarker(string[] markers, int expected) =>
        Assert.Equal(expected, FormPriority.FromMarkers(markers));

    [Fact]
    public void Band_KeepsSimilarFrequenciesTogetherAndOrdersTheRest()
    {
        // 紙 (nf03) and 髪 (nf06) tie, so example sentences can decide between them.
        Assert.Equal(FormPriority.Band(3), FormPriority.Band(6));
        // かみ: 紙 before 加味 (nf15) before 上 (ichi1 only) before an untagged form.
        Assert.True(FormPriority.Band(3) < FormPriority.Band(15));
        Assert.True(FormPriority.Band(15) < FormPriority.Band(FormPriority.CommonUnranked));
        Assert.True(FormPriority.Band(48) < FormPriority.Band(FormPriority.CommonUnranked));
        Assert.True(FormPriority.Band(FormPriority.CommonUnranked) < FormPriority.Band(FormPriority.Secondary));
        Assert.True(FormPriority.Band(FormPriority.Secondary) < FormPriority.Band(FormPriority.None));
    }
}

public class DictionaryEntryHeadwordTests
{
    private static DictionaryEntry Entry(string kanji, bool kanjiCommon, string reading, params string[] firstSenseMisc) =>
        new(1,
            [new KanjiForm(kanji, kanjiCommon)],
            [new ReadingForm(reading, true, true, [])],
            [new WordSense([], [], ["x"], [], firstSenseMisc)]);

    [Fact]
    public void UsuallyKanaWordWithRareKanji_LeadsWithTheReading()
    {
        var nagara = Entry("乍ら", kanjiCommon: false, "ながら", "uk");
        Assert.True(nagara.IsUsuallyKana);
        Assert.Equal("ながら", nagara.PrimaryText);
    }

    [Fact]
    public void UsuallyKanaWordWithCommonKanji_KeepsTheKanji()
    {
        var wakaru = Entry("分かる", kanjiCommon: true, "わかる", "uk");
        Assert.False(wakaru.IsUsuallyKana);
        Assert.Equal("分かる", wakaru.PrimaryText);
    }

    [Fact]
    public void WordNotTaggedUk_KeepsItsKanjiEvenIfRare()
    {
        var entry = Entry("佳味", kanjiCommon: false, "かみ");
        Assert.Equal("佳味", entry.PrimaryText);
    }

    [Fact]
    public void UkOnALaterSenseOnly_KeepsTheKanji()
    {
        // 会う is "uk" only for its "to have an accident" sense.
        var au = new DictionaryEntry(1,
            [new KanjiForm("遇う", false)],
            [new ReadingForm("あう", true, true, [])],
            [new WordSense([], [], ["to meet"], [], []), new WordSense([], [], ["to have (an accident)"], [], ["uk"])]);
        Assert.Equal("遇う", au.PrimaryText);
    }
}
