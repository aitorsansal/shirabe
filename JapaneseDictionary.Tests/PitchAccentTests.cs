using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Pitch;

using Xunit;

namespace JapaneseDictionary.Tests;

public class PitchAccentTests
{
    [Theory]
    [InlineData("はし", 2)]
    [InlineData("きゃく", 2)]
    [InlineData("がっこう", 4)]
    [InlineData("コーヒー", 4)]
    [InlineData("しゅっぱつ", 4)]
    [InlineData("ん", 1)]
    [InlineData("ファイル", 3)]
    [InlineData("", 0)]
    public void Morae_CountsKanaUnitsOfTime(string reading, int expected) =>
        Assert.Equal(expected, Morae.Count(reading));

    [Fact]
    public void Morae_SmallKanaJoinThePreviousMora()
    {
        Assert.Equal(["しゅ", "っ", "ぱ", "つ"], Morae.Split("しゅっぱつ"));
        Assert.Equal(["コ", "ー", "ヒ", "ー"], Morae.Split("コーヒー"));
        Assert.Equal(["フィ", "ル", "ム"], Morae.Split("フィルム"));
    }

    // Two morae plus the particle.
    [Theory]
    [InlineData(0, "LHH")]
    [InlineData(1, "HLL")]
    [InlineData(2, "LHL")]
    public void Pattern_TwoMorae(int accent, string expected) =>
        Assert.Equal(expected, PitchPattern.ToLetters(PitchPattern.Compute(2, accent)));

    // Three morae plus the particle.
    [Theory]
    [InlineData(0, "LHHH")]
    [InlineData(1, "HLLL")]
    [InlineData(2, "LHLL")]
    [InlineData(3, "LHHL")]
    public void Pattern_ThreeMorae(int accent, string expected) =>
        Assert.Equal(expected, PitchPattern.ToLetters(PitchPattern.Compute(3, accent)));

    [Fact]
    public void Pattern_OneMora()
    {
        Assert.Equal("LH", PitchPattern.ToLetters(PitchPattern.Compute(1, 0)));
        Assert.Equal("HL", PitchPattern.ToLetters(PitchPattern.Compute(1, 1)));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(2, -1)]
    public void Pattern_RejectsAccentsOutsideTheWord(int moraCount, int accent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchPattern.Compute(moraCount, accent));
}

/// <summary>Pitch accent and JLPT data against the real bundled dictionary.db3.</summary>
public class PitchAndJlptRepositoryTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private readonly DictionaryRepository? repo;

    public PitchAndJlptRepositoryTests()
    {
        if (File.Exists(DbPath))
            repo = new DictionaryRepository(DbPath);
    }

    public void Dispose() => repo?.Dispose();

    // Finds the entry written with this kanji and read as はし.
    private int HashiEntry(string kanji) =>
        repo!.FindByExactText(kanji).Single(id => repo.GetEntry(id)!.PrimaryText == kanji
            && repo.GetEntry(id)!.Readings.Any(r => r.Text == "はし"));

    [SkippableTheory]
    [InlineData("箸", 1)]
    [InlineData("橋", 2)]
    [InlineData("端", 0)]
    public void GetPitchAccents_HashiHomophonesDifferByAccent(string kanji, int accent)
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var accents = repo!.GetPitchAccents(HashiEntry(kanji));
        Assert.Equal(accent, accents.Single(a => a.Reading == "はし").Accents[0]);
    }

    [SkippableFact]
    public void GetPitchAccents_KeepsSeveralAccentsInSourceOrder()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        // 食 (しょく) is said both flat and with a drop after the first mora.
        var accents = repo!.GetPitchAccents(1956480);
        Assert.Equal([0, 1], accents.Single(a => a.Reading == "しょく").Accents);
    }

    [SkippableFact]
    public void GetPitchAccents_EntryWithoutDataIsEmpty()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.Empty(repo!.GetPitchAccents(1000000)); // ヽ, a repetition mark
    }

    [SkippableFact]
    public void GetJlptLevel_TaberuIsN5()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.Equal(5, repo!.GetJlptLevel(1358280));
        Assert.Equal(5, repo.GetJlptLevels([1358280, 1000000])[1358280]);
        Assert.DoesNotContain(1000000, repo.GetJlptLevels([1358280, 1000000]).Keys);
    }

    [SkippableFact]
    public void GetJlptLevel_UnlistedWordHasNone()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.Null(repo!.GetJlptLevel(1000000));
    }

    [SkippableFact]
    public void GetKanjiInfo_CommonKanjiHaveAJlptLevelOnTheCurrentScale()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.Equal(5, repo!.GetKanjiInfo("食")!.JlptN);
        Assert.Equal(5, repo.GetKanjiInfo("日")!.JlptN);
        Assert.Null(repo.GetKanjiInfo("鬱")!.JlptN);
        // The old four-level scale stays as KANJIDIC2 ships it.
        Assert.Equal(4, repo.GetKanjiInfo("食")!.JlptLevel);
    }
}
