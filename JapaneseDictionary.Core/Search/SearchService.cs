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
    /// <summary>A short kanji query found inside a longer headword (行 in 銀行).</summary>
    Contains,
}

/// <param name="ConjugationForm">For conjugated matches, the forms undone, innermost
/// first (e.g. "causative · passive · negative · past").</param>
/// <param name="MatchedText">What the entry matched on: the deinflected text for
/// conjugated matches, the kana form for romaji input, the gloss for English.</param>
public sealed record SearchResult(int EntryId, SearchMatchKind Kind, string? ConjugationForm, string? MatchedText = null);

/// <summary>
/// Single entry point for dictionary search: kanji, kana, romaji, conjugated
/// verb/adjective forms and English meanings, from one search box.
/// Text with any Japanese in it is searched as Japanese only. Latin text is
/// searched as English, and also as romaji when all of it converts to kana
/// ("taberu", "go", but not "eat"), with both merged into one list by match
/// strength: exact matches, then conjugations, whole-word English, Japanese
/// prefixes, and English substrings last.
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

    /// <summary>Up to this length an English query only matches whole words: "go"
    /// is inside tens of thousands of glosses ("good", "ago"), which is slow to
    /// rank and useless to show.</summary>
    private const int MaxWholeWordOnlyLength = 3;

    /// <summary>Longest kanji-only query that also searches for words containing
    /// it. Longer ones are specific enough that a containing match is noise
    /// (食堂 would otherwise list every 食堂車 and 学生食堂 ahead of the rest).</summary>
    private const int MaxCompoundQueryLength = 2;

    /// <summary>Slots the prefix tier keeps even when the exact tier already filled
    /// some, so compounds never squeeze it out entirely.</summary>
    private const int MinPrefixSlots = 5;

    // Loaded on first use; a race between two first searches only means
    // building the same small dictionary twice.
    private IReadOnlyDictionary<char, int>? kanjiFrequencyRanks;

    public IReadOnlyList<SearchResult> Search(string rawQuery, int limit = 50)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0) return [];

        kanjiFrequencyRanks ??= repo.GetKanjiFrequencyRanks();

        var japaneseForms = BuildJapaneseForms(query);
        var glosses = !ContainsJapanese(query) && query.Length >= MinGlossQueryLength
            ? GlossRanker.Rank(query, repo.FindGlossCandidates(query, wholeWordOnly: query.Length <= MaxWholeWordOnlyLength), kanjiFrequencyRanks, limit)
            : [];

        var results = new List<SearchResult>();
        var seen = new HashSet<int>();

        void Add(IEnumerable<SearchResult> tier)
        {
            foreach (var result in tier)
            {
                if (results.Count < limit && seen.Add(result.EntryId))
                    results.Add(result);
            }
        }

        Add(MergeByCommonness(
            FindExactMatches(japaneseForms),
            glosses.Where(g => g.MatchRank == 0).Select(GlossResult).ToList()));

        foreach (var form in japaneseForms.Where(ContainsJapanese))
            Add(FindConjugatedMatches(form));

        Add(glosses.Where(g => g.MatchRank == 1).Select(g => GlossResult(g).Result));

        // Kanji forms and readings are Japanese, so the raw Latin text never
        // prefixes one; skipping it saves two table scans per keystroke.
        // A bare kanji or kanji pair ("行", "食堂") also finds compounds that merely
        // contain it (銀行). Common kanji have hundreds of prefix matches that would
        // fill the whole list first, so those leave room for the compounds.
        var findsCompounds = IsShortKanjiQuery(query);
        foreach (var form in japaneseForms.Where(ContainsJapanese))
        {
            if (results.Count >= limit) break;
            var prefixLimit = findsCompounds ? Math.Max(results.Count + MinPrefixSlots, limit / 2) : limit;
            Add(repo.FindByPrefix(form, prefixLimit - results.Count).Select(id => new SearchResult(id, SearchMatchKind.Prefix, null, form)));
        }

        if (findsCompounds)
            Add(repo.FindCompoundsByKanji(query, limit).Select(id => new SearchResult(id, SearchMatchKind.Contains, null, query)));

        Add(glosses.Where(g => g.MatchRank == 2).Select(g => GlossResult(g).Result));

        return results;
    }

    private static (SearchResult Result, bool IsCommon, int Rarity) GlossResult(RankedGloss gloss) =>
        (new SearchResult(gloss.EntryId, SearchMatchKind.Gloss, null, gloss.GlossText), gloss.IsCommon, gloss.Rarity);

    /// <summary>Exact kanji/reading matches for each form, common and everyday
    /// words first (かみ: 紙 and 神 before rarer homophones; なる: 成る before 生る).</summary>
    private List<(SearchResult Result, bool IsCommon, int Rarity)> FindExactMatches(IReadOnlyList<string> forms)
    {
        var formOrder = forms.Select((form, i) => (form, i)).ToDictionary(x => x.form, x => x.i);
        var matches = repo.FindByExactTexts(forms.ToList())
            .GroupBy(m => m.EntryId)
            .Select(g => (Match: g.OrderBy(m => formOrder[m.Text]).First(), IsCommon: g.Any(m => m.IsCommon && m.FormIsCommon)))
            .ToList();
        var exampleCounts = repo.GetExampleCounts(matches.Select(x => x.Match.EntryId).ToList());
        return matches
            .Select(x => (
                Result: new SearchResult(x.Match.EntryId, SearchMatchKind.ExactMatch, null, x.Match.Text),
                x.IsCommon,
                Examples: ExampleEvidence.Band(exampleCounts.GetValueOrDefault(x.Match.EntryId)),
                Rarity: KanjiRarity.Of(x.Match.PrimaryText, kanjiFrequencyRanks!)))
            .OrderBy(x => x.IsCommon ? 0 : 1)
            .ThenByDescending(x => x.Examples)
            .ThenBy(x => x.Rarity)
            .Select(x => (x.Result, x.IsCommon, x.Rarity))
            .ToList();
    }

    /// <summary>Merges two already-ranked lists, taking whichever head is more
    /// common/everyday, so each keeps its own internal order (the gloss ranker's
    /// sense and qualifier ordering isn't flattened).</summary>
    private static IEnumerable<SearchResult> MergeByCommonness(
        List<(SearchResult Result, bool IsCommon, int Rarity)> first,
        List<(SearchResult Result, bool IsCommon, int Rarity)> second)
    {
        int i = 0, j = 0;
        while (i < first.Count || j < second.Count)
        {
            var takeFirst = j >= second.Count
                || (i < first.Count && (first[i].IsCommon ? 0 : 1, first[i].Rarity).CompareTo((second[j].IsCommon ? 0 : 1, second[j].Rarity)) <= 0);
            yield return takeFirst ? first[i++].Result : second[j++].Result;
        }
    }

    /// <summary>The literal query, plus its kana conversions when it reads as
    /// romaji — so typing "tabeta" on a normal keyboard still finds たべた. Only a
    /// full conversion counts: "eat" would leave a stray t, so it's English only.</summary>
    private static List<string> BuildJapaneseForms(string query)
    {
        var forms = new List<string> { query };
        if (LooksLikeRomaji(query))
        {
            var hiragana = RomajiConverter.ToHiragana(query);
            if (!hiragana.Any(char.IsAsciiLetter))
            {
                forms.Add(hiragana);
                forms.Add(RomajiConverter.ToKatakana(query));
            }
        }

        return forms;
    }

    /// <summary>Dictionary entries <paramref name="form"/> is a conjugation of,
    /// best first: fewest conjugation steps, then common words, then
    /// <see cref="ExampleEvidence"/>, then <see cref="KanjiRarity"/>.</summary>
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

        var exactMatches = repo.FindByExactTexts(lookups.Select(l => l.Text).Distinct().ToList());
        var matchesByText = exactMatches.ToLookup(m => m.Text);
        var exampleCounts = repo.GetExampleCounts(exactMatches.Select(m => m.EntryId).Distinct().ToList());
        kanjiFrequencyRanks ??= repo.GetKanjiFrequencyRanks();

        var best = new Dictionary<int, ((int Steps, int NotCommon, int FewerExamples, int Rarity) Rank, Deinflection Source)>();
        foreach (var (text, source) in lookups)
        {
            foreach (var match in matchesByText[text])
            {
                if (!match.WordClasses.Any(wc => FitsInflectionType(wc, source.Type, text))) continue;

                var rank = (
                    source.Reasons.Count,
                    match.IsCommon && match.FormIsCommon ? 0 : 1,
                    -ExampleEvidence.Band(exampleCounts.GetValueOrDefault(match.EntryId)),
                    KanjiRarity.Of(match.PrimaryText, kanjiFrequencyRanks));
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

    private static bool IsShortKanjiQuery(string text) =>
        text.Length <= MaxCompoundQueryLength && text.All(c => c is >= '一' and <= '鿿');

    private static bool ContainsJapanese(string text) =>
        text.Any(c => c is >= '぀' and <= 'ヿ' or >= '一' and <= '鿿');

    [GeneratedRegex(@"^[a-zA-Z']+$")]
    private static partial Regex RomajiOnly();

}
