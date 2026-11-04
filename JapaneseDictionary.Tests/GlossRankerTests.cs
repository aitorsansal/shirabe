using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;

using Xunit;

namespace JapaneseDictionary.Tests;

public class GlossRankerTests
{
    private static GlossCandidateRow Candidate(int entryId, string gloss, int senseOrder, int priority, int? jlpt = null) =>
        new() { EntryId = entryId, GlossText = gloss, SenseOrder = senseOrder, GlossOrder = 1, Priority = priority, JlptLevel = jlpt, PrimaryText = "x" };

    private static List<int> Rank(params GlossCandidateRow[] candidates) =>
        GlossRanker.Rank("eat", candidates, new Dictionary<char, int>(), 10).Select(r => r.EntryId).ToList();

    [Fact]
    public void FirstSenseMatch_BeatsLaterSenseWithinTheSameBand()
    {
        // Same band; entry 1 matches in sense 7, entry 2 in sense 1.
        Assert.Equal([2, 1], Rank(Candidate(1, "to eat", 7, 3), Candidate(2, "to eat", 1, 3)));
    }

    [Fact]
    public void LaterSenseMatch_LosesToANearlyAsCommonFirstSenseMatch()
    {
        // nf03 in sense 7 (band 0, +2) vs nf12 in sense 1 (band 1).
        Assert.Equal([2, 1], Rank(Candidate(1, "to eat", 7, 3), Candidate(2, "to eat", 1, 12)));
    }

    [Fact]
    public void LaterSenseMatch_OfABasicWord_BeatsARareFirstSenseMatch()
    {
        // 足 "leg" (sense 2, nf01) over 脚部 "leg" (sense 1, no frequency tag).
        Assert.Equal([1, 2], Rank(Candidate(1, "to eat", 2, 1), Candidate(2, "to eat", 1, FormPriority.None)));
    }

    [Fact]
    public void MatchRank_StillBeatsTheSensePenalty()
    {
        Assert.Equal([1, 2], Rank(Candidate(1, "to eat", 5, 3), Candidate(2, "to eat away", 1, 3)));
    }

    [Fact]
    public void TrailingPreposition_CountsAsANarrowedExactMatch()
    {
        // "to eat at" beats a whole-word match, but loses to a plain "to eat".
        Assert.Equal([3, 1, 2], Rank(
            Candidate(1, "to eat at", 1, 3),
            Candidate(2, "to eat out", 1, 3),
            Candidate(3, "to eat", 1, 3)));
    }

    [Fact]
    public void JlptLevel_LiftsAnUntaggedBasicWord()
    {
        // ichi1-only N5 word vs nf20 word: JLPT puts the N5 word first.
        var ranked = GlossRanker.Rank("eat",
            [Candidate(1, "to eat", 1, 20), Candidate(2, "to eat", 1, FormPriority.CommonUnranked, jlpt: 5)],
            new Dictionary<char, int>(), 10);
        Assert.Equal([2, 1], ranked.Select(r => r.EntryId));
        Assert.Equal(0, ranked[0].PriorityBand);
    }

    [Fact]
    public void PriorityBand_IncludesTheSensePenalty()
    {
        // SearchService merges gloss matches with exact Japanese matches by this band,
        // so a side meaning must not compete as a top-band word there (気 has the
        // gloss "ki" in a late sense and must not jump 木 for rōmaji "ki").
        var ranked = GlossRanker.Rank("eat",
            [Candidate(1, "to eat", 1, 20), Candidate(2, "to eat", 3, 12)],
            new Dictionary<char, int>(), 10);
        Assert.Equal(2, ranked.Single(r => r.EntryId == 1).PriorityBand);
        Assert.Equal(1 + 5, ranked.Single(r => r.EntryId == 2).PriorityBand);
    }

    [Fact]
    public void ExampleCount_BreaksTiesBetweenEquallyGoodExactGlosses()
    {
        var counts = new Dictionary<int, int> { [1] = 25, [2] = 5000 };
        var ranked = GlossRanker.Rank("eat",
            [Candidate(1, "to eat", 1, 3), Candidate(2, "to eat", 1, 3)],
            new Dictionary<char, int>(), 10, _ => counts);
        Assert.Equal([2, 1], ranked.Select(r => r.EntryId));
    }

    [Fact]
    public void ExampleCount_DoesNotOverrideTheBandOrSensePenalty()
    {
        // 1: nf03 in sense 2 (band 2 after the penalty); 2: nf40 (band 4); 3: nf03 (band 0).
        var counts = new Dictionary<int, int> { [1] = 5000, [2] = 5000, [3] = 0 };
        var ranked = GlossRanker.Rank("eat",
            [Candidate(1, "to eat", 2, 3), Candidate(2, "to eat", 1, 40), Candidate(3, "to eat", 1, 3)],
            new Dictionary<char, int>(), 10, _ => counts);
        Assert.Equal([3, 1, 2], ranked.Select(r => r.EntryId));
    }

    [Fact]
    public void ThirdSenseOnward_CostsMoreThanTheSecond()
    {
        // nf03 in sense 2 (band 2) beats nf33 in sense 1 (band 4); nf03 in sense 3 (band 5) doesn't.
        Assert.Equal([1, 2, 3], Rank(Candidate(1, "to eat", 2, 3), Candidate(2, "to eat", 1, 33), Candidate(3, "to eat", 3, 3)));
    }
}
