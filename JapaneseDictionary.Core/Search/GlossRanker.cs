using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Orders English gloss matches so that searching "eat" finds 食べる rather than
/// whatever happens to contain "b<i>eat</i>". Each entry is ranked by its best
/// gloss: an exact gloss ("eat" / "to eat", ignoring bracketed qualifiers),
/// then a whole-word match, then a bare substring.
/// A trailing preposition still counts as exact ("to arrive at" for "arrive",
/// "to search for" for "search"), but as a narrowed one.
/// Within a match rank, frequency decides (<see cref="FormPriority.EffectiveBand"/>,
/// so very basic words JMdict leaves without an nf tag still count by their JLPT
/// level), with a <see cref="LaterSensePenalty"/> for matches outside the first
/// sense: 食べる (sense 1) outranks 取る (sense 7 of 18) for "eat".
/// Among exact glosses, a narrowing qualifier ("school (of fish)", "to run (a
/// business)") ranks below an unqualified or merely descriptive one ("water
/// (esp. cool or cold)", "dog (Canis familiaris)"). Among equally good exact
/// glosses, the entry with more example sentences (<see cref="ExampleEvidence"/>)
/// wins, the same tiebreak exact Japanese matches use (する over 致す, やる and
/// なさる for "do"). Remaining ties go to earlier senses and glosses, then
/// <see cref="KanjiRarity"/>.
/// </summary>
/// <param name="MatchRank">0 exact gloss, 1 whole word, 2 substring.</param>
/// <param name="PriorityBand">The entry's frequency band plus the sense penalty of
/// the matched gloss, comparable with exact Japanese matches' bands.</param>
internal sealed record RankedGloss(int EntryId, int MatchRank, string GlossText, int PriorityBand, int Rarity);

internal static partial class GlossRanker
{
    /// <summary>SenseRow.SenseOrder is 1-based (DataBuilder numbers senses from 1).</summary>
    private const int FirstSenseOrder = 1;

    /// <summary>Bands a match outside the entry's first sense loses. JMdict lists
    /// core meanings first, so a second sense costs little: a basic word's sense 2
    /// (足 "leg", 起きる "to wake up") still beats rare words that mean it first
    /// (脚部, 目を覚ます). Later senses are side meanings of common words (取る "to
    /// eat", sense 7; 重い "blue") and cost enough to stay out of the way.</summary>
    private static int LaterSensePenalty(int senseOrder) => senseOrder switch
    {
        <= FirstSenseOrder => 0,
        FirstSenseOrder + 1 => 2,
        _ => 5,
    };

    public static IReadOnlyList<RankedGloss> Rank(
        string query,
        IEnumerable<GlossCandidateRow> candidates,
        IReadOnlyDictionary<char, int> kanjiFrequencyRanks,
        int limit,
        Func<IReadOnlyCollection<int>, IReadOnlyDictionary<int, int>>? exampleCountsOf = null)
    {
        // Everything is compared accent-folded ("cafe" is "café"); the original
        // gloss is what gets returned.
        var normalizedQuery = TextFolding.Fold(query.Trim());
        var toForm = "to " + normalizedQuery;
        var wholeWord = new Regex(
            $"(?<![a-z0-9]){Regex.Escape(normalizedQuery)}(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var withPreposition = new Regex(
            $"^(?:to )?{Regex.Escape(normalizedQuery)} (?:at|for|to|with|about|from|of)$",
            RegexOptions.CultureInvariant);

        var best = new Dictionary<int, ((int MatchRank, int RankedBand, int Narrowing, int SenseOrder, int GlossOrder, int Rarity, int Length, int EntryId) Key, string Gloss)>();
        foreach (var candidate in candidates)
        {
            var gloss = candidate.GlossText;
            var core = TextFolding.Fold(StripQualifiers(gloss));

            var isPlainExact = core == normalizedQuery || core == toForm;
            var isPrepositionExact = !isPlainExact && withPreposition.IsMatch(core);
            var isExact = isPlainExact || isPrepositionExact;
            var matchRank = isExact ? 0
                : wholeWord.IsMatch(TextFolding.Fold(gloss)) ? 1
                : 2;

            var band = FormPriority.EffectiveBand(candidate.Priority, candidate.JlptLevel);
            var key = (
                matchRank,
                band + LaterSensePenalty(candidate.SenseOrder),
                isExact && (isPrepositionExact || HasNarrowingQualifier(gloss)) ? 1 : 0,
                candidate.SenseOrder,
                candidate.GlossOrder,
                KanjiRarity.Of(candidate.PrimaryText, kanjiFrequencyRanks),
                core.Length,
                candidate.EntryId);

            if (!best.TryGetValue(candidate.EntryId, out var existing) || key.CompareTo(existing.Key) < 0)
                best[candidate.EntryId] = (key, candidate.GlossText);
        }

        // Example-sentence counts only break ties between exact glosses: that set
        // stays small (a few hundred entries at most), while whole-word and
        // substring matches can run into thousands of entries.
        var exactIds = best.Values.Where(v => v.Key.MatchRank == 0).Select(v => v.Key.EntryId).ToList();
        var exampleCounts = exampleCountsOf is not null && exactIds.Count > 0
            ? exampleCountsOf(exactIds)
            : new Dictionary<int, int>();

        return best.Values
            .OrderBy(v => (v.Key.MatchRank, v.Key.RankedBand, v.Key.Narrowing))
            .ThenByDescending(v => ExampleEvidence.Band(exampleCounts.GetValueOrDefault(v.Key.EntryId)))
            .ThenBy(v => v.Key)
            .Take(limit)
            .Select(v => new RankedGloss(v.Key.EntryId, v.Key.MatchRank, v.Gloss, v.Key.RankedBand, v.Key.Rarity))
            .ToList();
    }

    private static string StripQualifiers(string gloss)
    {
        // Loop so nested brackets collapse too: "dog (Canis (lupus) familiaris)".
        string previous;
        do
        {
            previous = gloss;
            gloss = InnermostBrackets().Replace(gloss, "");
        }
        while (gloss != previous);

        return Whitespace().Replace(gloss, " ").Trim();
    }

    private static bool HasNarrowingQualifier(string gloss) =>
        TopLevelBrackets().Matches(gloss).Any(m => !DescriptiveQualifier().IsMatch(m.Value));

    [GeneratedRegex(@"\([^()]*\)")]
    private static partial Regex InnermostBrackets();

    [GeneratedRegex(@"\([^()]*(?:\([^()]*\)[^()]*)*\)")]
    private static partial Regex TopLevelBrackets();

    // JMdict's descriptive asides ("(esp. ...)", "(e.g. ...)") and scientific or
    // proper names ("(Canis familiaris)") add detail without narrowing the meaning.
    [GeneratedRegex(@"^\((?:esp|e\.g|i\.e|usu|incl|lit|orig|occ|sometimes|often)\b|^\([A-Z]")]
    private static partial Regex DescriptiveQualifier();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
