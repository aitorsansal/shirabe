using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Data.Schema;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Orders English gloss matches so that searching "eat" finds 食べる rather than
/// whatever happens to contain "b<i>eat</i>". Each entry is ranked by its best
/// gloss: an exact gloss ("eat" / "to eat", ignoring bracketed qualifiers),
/// then a whole-word match, then a bare substring.
/// Among exact glosses, a narrowing qualifier ("school (of fish)", "to run (a
/// business)") ranks below an unqualified or merely descriptive one ("water
/// (esp. cool or cold)", "dog (Canis familiaris)"). Remaining ties go to common
/// words, earlier senses and glosses, then <see cref="KanjiRarity"/>.
/// </summary>
internal static partial class GlossRanker
{
    public static IReadOnlyList<int> Rank(
        string query,
        IEnumerable<GlossCandidateRow> candidates,
        IReadOnlyDictionary<char, int> kanjiFrequencyRanks,
        int limit)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var toForm = "to " + normalizedQuery;
        var wholeWord = new Regex(
            $"(?<![a-z0-9]){Regex.Escape(normalizedQuery)}(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var best = new Dictionary<int, (int, int, int, int, int, int, int, int)>();
        foreach (var candidate in candidates)
        {
            var gloss = candidate.GlossText;
            var core = StripQualifiers(gloss).ToLowerInvariant();

            var isExact = core == normalizedQuery || core == toForm;
            var matchRank = isExact ? 0
                : wholeWord.IsMatch(gloss) ? 1
                : 2;

            var key = (
                matchRank,
                candidate.IsCommon ? 0 : 1,
                isExact && HasNarrowingQualifier(gloss) ? 1 : 0,
                candidate.SenseOrder,
                candidate.GlossOrder,
                KanjiRarity.Of(candidate.PrimaryText, kanjiFrequencyRanks),
                core.Length,
                candidate.EntryId);

            if (!best.TryGetValue(candidate.EntryId, out var existing) || key.CompareTo(existing) < 0)
                best[candidate.EntryId] = key;
        }

        return best.Values.Order().Take(limit).Select(key => key.Item8).ToList();
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
