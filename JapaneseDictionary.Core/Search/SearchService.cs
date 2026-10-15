using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

public enum SearchMatchKind
{
    ExactMatch,
    Prefix,
    Conjugated,
    Gloss,
}

/// <param name="ConjugationForm">For conjugated matches, the forms undone, innermost
/// first (e.g. "causative · passive · negative · past").</param>
/// <param name="ConjugatedText">For conjugated matches, the text that was
/// deinflected: the query itself, or its kana conversion for romaji input.</param>
public sealed record SearchResult(int EntryId, SearchMatchKind Kind, string? ConjugationForm, string? ConjugatedText = null);

public enum SearchMode { Japanese, English }

/// <summary>
/// Single entry point for dictionary search: kanji, kana (with romaji
/// auto-conversion), conjugated verb/adjective forms, and English meanings.
/// </summary>
public sealed partial class SearchService
{
    private readonly DictionaryRepository repo;

    public SearchService(DictionaryRepository repo) => this.repo = repo;

    /// <summary>Minimum query length before an (unindexed, substring) gloss
    /// search is attempted — a 1-character English substring matches such a
    /// huge fraction of the ~440k gloss rows that it's a multi-second table
    /// scan for essentially no useful results.</summary>
    private const int MinGlossQueryLength = 2;

    // Loaded on first use; a race between two first searches only means
    // building the same small dictionary twice.
    private IReadOnlyDictionary<char, int>? kanjiFrequencyRanks;

    public IReadOnlyList<SearchResult> Search(string rawQuery, int limit = 50) =>
        Search(rawQuery, SearchMode.Japanese, limit);

    public IReadOnlyList<SearchResult> Search(string rawQuery, SearchMode mode, int limit = 50)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0) return [];

        var results = new List<SearchResult>();
        var seen = new HashSet<int>();

        void AddAll(IEnumerable<int> ids, SearchMatchKind kind)
        {
            foreach (var id in ids)
            {
                if (seen.Add(id))
                    results.Add(new SearchResult(id, kind, null));
            }
        }

        var queryForms = BuildQueryForms(query);

        foreach (var form in queryForms)
            AddAll(repo.FindByExactText(form), SearchMatchKind.ExactMatch);

        if (mode == SearchMode.Japanese)
        {
            foreach (var form in queryForms.Where(ContainsJapanese))
            {
                foreach (var match in FindConjugatedMatches(form))
                {
                    if (seen.Add(match.EntryId))
                        results.Add(match);
                }
            }

            if (results.Count < limit)
            {
                foreach (var form in queryForms)
                    AddAll(repo.FindByPrefix(form, limit - results.Count), SearchMatchKind.Prefix);
            }
        }

        // Gloss search is gated to English mode (per the mode toggle's whole
        // purpose — "which field the query matches against") rather than a
        // "does this look like English" guess: any input mode always has some
        // ASCII in it (romaji, punctuation), so that heuristic previously ran
        // an expensive substring scan on every keystroke regardless of mode.
        if (mode == SearchMode.English && results.Count < limit && query.Length >= MinGlossQueryLength)
        {
            kanjiFrequencyRanks ??= repo.GetKanjiFrequencyRanks();
            var ranked = GlossRanker.Rank(query, repo.FindGlossCandidates(query), kanjiFrequencyRanks, limit - results.Count);
            AddAll(ranked, SearchMatchKind.Gloss);
        }

        return results.Take(limit).ToList();
    }

    /// <summary>The literal query, plus its romaji-to-kana conversions (hiragana
    /// and katakana) when it looks like romaji — so typing "tabeta" on a normal
    /// keyboard still finds たべた without switching input methods.</summary>
    private static List<string> BuildQueryForms(string query)
    {
        var forms = new List<string> { query };
        if (LooksLikeRomaji(query))
        {
            forms.Add(RomajiConverter.ToHiragana(query));
            forms.Add(RomajiConverter.ToKatakana(query));
        }

        return forms;
    }

    /// <summary>Dictionary entries <paramref name="form"/> is a conjugation of,
    /// best first: fewest conjugation steps, then common words, then
    /// <see cref="KanjiRarity"/>.</summary>
    private List<SearchResult> FindConjugatedMatches(string form)
    {
        var deinflections = Deinflector.Deinflect(form)
            .Where(d => (d.Type & InflectionType.DictionaryForm) != 0)
            .ToList();
        if (deinflections.Count == 0) return [];

        // 勉強した deinflects to 勉強する, but JMdict lists the noun 勉強 tagged as
        // taking する rather than a separate 勉強する entry, so look up both.
        var lookups = new List<(string Text, Deinflection Source)>();
        foreach (var d in deinflections)
        {
            lookups.Add((d.Text, d));
            if ((d.Type & InflectionType.Suru) != 0 && d.Text.Length > 2 && d.Text.EndsWith("する", StringComparison.Ordinal))
                lookups.Add((d.Text[..^2], d));
        }

        var matchesByText = repo.FindByExactTexts(lookups.Select(l => l.Text).Distinct().ToList())
            .ToLookup(m => m.Text);
        kanjiFrequencyRanks ??= repo.GetKanjiFrequencyRanks();

        var best = new Dictionary<int, ((int Steps, int NotCommon, int Rarity) Rank, Deinflection Source)>();
        foreach (var (text, source) in lookups)
        {
            foreach (var match in matchesByText[text])
            {
                if (!match.WordClasses.Any(wc => FitsInflectionType(wc, source.Type, text))) continue;

                var rank = (source.Reasons.Count, match.IsCommon ? 0 : 1, KanjiRarity.Of(match.PrimaryText, kanjiFrequencyRanks));
                if (!best.TryGetValue(match.EntryId, out var existing) || rank.CompareTo(existing.Rank) < 0)
                    best[match.EntryId] = (rank, source);
            }
        }

        return best
            .OrderBy(kv => kv.Value.Rank)
            .ThenBy(kv => kv.Key)
            .Select(kv => new SearchResult(
                kv.Key,
                SearchMatchKind.Conjugated,
                DescribeConjugation(kv.Value.Source.Reasons),
                form))
            .ToList();
    }

    // Reasons arrive outermost first; show them innermost first. A て-form that
    // only connects to an auxiliary (食べて|いる) isn't worth naming on its own.
    private static string DescribeConjugation(IReadOnlyList<string> reasons)
    {
        var innermostFirst = reasons.Reverse().ToList();
        return string.Join(" · ", innermostFirst.Where((reason, i) => reason != "te-form" || i == innermostFirst.Count - 1));
    }

    /// <summary>Whether an entry of class <paramref name="wordClass"/> can be the
    /// dictionary form <paramref name="text"/> that deinflection produced as
    /// <paramref name="type"/>. Godan verbs also need the matching final kana, so
    /// a -ku rule can't land on a -mu verb that happens to share the text.</summary>
    private static bool FitsInflectionType(WordClass wordClass, InflectionType type, string text) => wordClass switch
    {
        WordClass.IchidanVerb => (type & InflectionType.Ichidan) != 0,
        WordClass.KuruVerb => (type & InflectionType.Kuru) != 0,
        WordClass.SuruVerb => (type & InflectionType.Suru) != 0,
        WordClass.IAdjective => (type & InflectionType.IAdjective) != 0,
        WordClass.NaAdjective => (type & InflectionType.NaAdjective) != 0,
        _ => (type & InflectionType.Godan) != 0 && GodanEnding(wordClass) is { } ending && text.EndsWith(ending, StringComparison.Ordinal),
    };

    private static string? GodanEnding(WordClass wordClass) => wordClass switch
    {
        WordClass.GodanVerbU => "う",
        WordClass.GodanVerbKu or WordClass.GodanVerbIkuYuku => "く",
        WordClass.GodanVerbGu => "ぐ",
        WordClass.GodanVerbSu => "す",
        WordClass.GodanVerbTsu => "つ",
        WordClass.GodanVerbNu => "ぬ",
        WordClass.GodanVerbBu => "ぶ",
        WordClass.GodanVerbMu => "む",
        WordClass.GodanVerbRu => "る",
        _ => null,
    };

    private static bool LooksLikeRomaji(string text) => RomajiOnly().IsMatch(text);

    private static bool ContainsJapanese(string text) =>
        text.Any(c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿');

    [GeneratedRegex(@"^[a-zA-Z']+$")]
    private static partial Regex RomajiOnly();

}
