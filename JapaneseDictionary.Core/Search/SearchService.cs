using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

public enum SearchMatchKind
{
    ExactMatch,
    Prefix,
    Conjugated,
    Gloss,
    /// <summary>A Spanish/French/German meaning; <see cref="SearchResult.MatchedLanguage"/> says which.</summary>
    TranslatedGloss,
    /// <summary>A short kanji query found inside a longer headword (行 in 銀行).</summary>
    Contains,
    /// <summary>A kanji form or reading matching a <c>?</c>/<c>*</c> pattern; the
    /// query skips every other tier.</summary>
    Wildcard,
}

/// <param name="ConjugationForm">For conjugated matches, the forms undone, innermost
/// first (e.g. "causative · passive · negative · past").</param>
/// <param name="MatchedText">What the entry matched on: the deinflected text for
/// conjugated matches, the kana form for romaji input, the original gloss for
/// English and translated meanings.</param>
/// <param name="MatchedLanguage">For translated meanings, the language code ("spa").</param>
public sealed record SearchResult(int EntryId, SearchMatchKind Kind, string? ConjugationForm, string? MatchedText = null, string? MatchedLanguage = null);

/// <summary>
/// Single entry point for dictionary search: kanji, kana, romaji, conjugated
/// verb/adjective forms and English meanings (plus the user's chosen Spanish,
/// French and German ones), from one search box.
/// Text with any Japanese in it is searched as Japanese only. Latin text is
/// searched as English, and also as romaji when all of it converts to kana
/// ("taberu", "go", but not "eat"), with everything merged into one list by match
/// strength: exact matches (Japanese, English and translated ones interleaved by
/// commonness), then conjugations, whole-word English, whole-word translated,
/// Japanese prefixes, English substrings and translated substrings last.
/// Translated meanings are matched ignoring accents ("cafe" finds "café").
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

    /// <param name="translationLanguages">Codes of extra languages whose meanings are
    /// searched too (<see cref="TranslationLanguages"/>); null or empty searches English only.</param>
    public IReadOnlyList<SearchResult> Search(string rawQuery, int limit = 50, IReadOnlyCollection<string>? translationLanguages = null)
    {
        var query = rawQuery.Trim().Replace('＊', '*').Replace('？', '?');
        if (query.Length == 0) return [];

        if (query.IndexOfAny(['*', '?']) >= 0)
        {
            var literal = query.Replace("*", "").Replace("?", "");
            if (literal.Length == 0) return [];

            var patterns = new List<string>();
            if (ContainsJapanese(query))
                patterns.Add(query);
            else if (LooksLikeRomaji(literal) && !RomajiConverter.ToHiragana(literal).Any(char.IsAsciiLetter))
            {
                var hiragana = RomajiConverter.ToHiragana(query);
                if (!hiragana.Any(char.IsAsciiLetter))
                {
                    patterns.Add(hiragana);
                    patterns.Add(RomajiConverter.ToKatakana(query));
                }
            }

            // A literal [ would open a GLOB bracket expression; * and ? stay wildcards.
            if (patterns.Count > 0)
                return repo.FindByPattern(patterns.Select(p => p.Replace("[", "[[]")).ToList(), limit)
                    .Select(id => new SearchResult(id, SearchMatchKind.Wildcard, null, query))
                    .ToList();
        }

        kanjiFrequencyRanks ??= repo.GetKanjiFrequencyRanks();

        var japaneseForms = BuildJapaneseForms(query);
        var searchesGlosses = !ContainsJapanese(query) && query.Length >= MinGlossQueryLength;
        var glosses = searchesGlosses
            ? GlossRanker.Rank(query, repo.FindGlossCandidates(query, wholeWordOnly: query.Length <= MaxWholeWordOnlyLength), kanjiFrequencyRanks, limit, repo.GetExampleCounts)
            : [];

        // In TranslationLanguages.All order, so ties and tiers are stable.
        var translated = new List<(string Lang, IReadOnlyList<RankedGloss> Ranked)>();
        if (searchesGlosses && translationLanguages is { Count: > 0 })
        {
            foreach (var language in TranslationLanguages.All.Where(l => translationLanguages.Contains(l.Code)))
                translated.Add((language.Code, GlossRanker.Rank(query, FindTranslatedCandidates(query, language.Code), kanjiFrequencyRanks, limit, repo.GetExampleCounts)));
        }

        List<(SearchResult Result, int PriorityBand, int Rarity)> TranslatedTier(int matchRank) =>
            translated.SelectMany(t => t.Ranked.Where(g => g.MatchRank == matchRank).Select(g => GlossResult(g, t.Lang))).ToList();

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

        var exactLists = new List<List<(SearchResult Result, int PriorityBand, int Rarity)>>
        {
            FindExactMatches(japaneseForms),
            glosses.Where(g => g.MatchRank == 0).Select(g => GlossResult(g)).ToList(),
        };
        exactLists.AddRange(translated.Select(t => t.Ranked.Where(g => g.MatchRank == 0).Select(g => GlossResult(g, t.Lang)).ToList()));
        Add(MergeByCommonness(exactLists));

        foreach (var form in japaneseForms.Where(ContainsJapanese))
            Add(FindConjugatedMatches(form));

        Add(glosses.Where(g => g.MatchRank == 1).Select(g => GlossResult(g).Result));
        Add(TranslatedTier(1).Select(x => x.Result));

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
        Add(TranslatedTier(2).Select(x => x.Result));

        return results;
    }

    private static (SearchResult Result, int PriorityBand, int Rarity) GlossResult(RankedGloss gloss, string? language = null) =>
        (new SearchResult(gloss.EntryId, language == null ? SearchMatchKind.Gloss : SearchMatchKind.TranslatedGloss, null, gloss.GlossText, language), gloss.PriorityBand, gloss.Rarity);

    /// <summary>Glosses of <paramref name="lang"/> that contain the query ignoring
    /// case and accents, one candidate per gloss (the table stores a sense's glosses
    /// joined). Like the English search, short queries only match whole words.</summary>
    private IEnumerable<GlossCandidateRow> FindTranslatedCandidates(string query, string lang)
    {
        var folded = TextFolding.Fold(query);
        var wholeWordOnly = query.Length <= MaxWholeWordOnlyLength;
        var wholeWord = wholeWordOnly
            ? new Regex($"(?<![a-z0-9]){Regex.Escape(folded)}(?![a-z0-9])", RegexOptions.CultureInvariant)
            : null;

        foreach (var row in repo.FindTranslatedGlossCandidates(TextFolding.AccentInsensitiveLikePattern(query), TextFolding.AccentInsensitiveGlobPattern(query, wholeWordOnly), lang, wholeWordOnly))
        {
            var glosses = row.GlossesJoined.Split(TranslatedSenseRow.GlossSeparator);
            for (var i = 0; i < glosses.Length; i++)
            {
                var foldedGloss = TextFolding.Fold(glosses[i]);
                if (!foldedGloss.Contains(folded, StringComparison.Ordinal)) continue;
                if (wholeWord != null && !wholeWord.IsMatch(foldedGloss)) continue;

                yield return new GlossCandidateRow
                {
                    EntryId = row.EntryId,
                    GlossText = glosses[i],
                    SenseOrder = row.SenseOrder,
                    GlossOrder = i + 1,
                    IsCommon = row.IsCommon,
                    Priority = row.Priority,
                    PrimaryText = row.PrimaryText,
                    JlptLevel = row.JlptLevel,
                };
            }
        }
    }

    /// <summary>Exact kanji/reading matches for each form, most frequent first by
    /// the matched form's <see cref="FormPriority.EffectiveBand"/>, then
    /// <see cref="ExampleEvidence"/>, then <see cref="KanjiRarity"/> (かみ: 紙, 神
    /// and 髪 before 加味, and those before 上 read as かみ; なる: 成る before 生る).</summary>
    private List<(SearchResult Result, int PriorityBand, int Rarity)> FindExactMatches(IReadOnlyList<string> forms)
    {
        var formOrder = forms.Select((form, i) => (form, i)).ToDictionary(x => x.form, x => x.i);
        var matches = repo.FindByExactTexts(forms.ToList())
            .GroupBy(m => m.EntryId)
            .Select(g => (Match: g.OrderBy(m => formOrder[m.Text]).First(), PriorityBand: g.Min(m => m.EffectiveBand)))
            .ToList();
        var exampleCounts = repo.GetExampleCounts(matches.Select(x => x.Match.EntryId).ToList());
        return matches
            .Select(x => (
                Result: new SearchResult(x.Match.EntryId, SearchMatchKind.ExactMatch, null, x.Match.Text),
                x.PriorityBand,
                Examples: ExampleEvidence.Band(exampleCounts.GetValueOrDefault(x.Match.EntryId)),
                Rarity: KanjiRarity.Of(x.Match.PrimaryText, kanjiFrequencyRanks!)))
            .OrderBy(x => x.PriorityBand)
            .ThenByDescending(x => x.Examples)
            .ThenBy(x => x.Rarity)
            .Select(x => (x.Result, x.PriorityBand, x.Rarity))
            .ToList();
    }

    /// <summary>Merges already-ranked lists, always taking whichever head is more
    /// frequent/everyday (ties go to the earlier list), so each keeps its own
    /// internal order (the gloss ranker's sense and qualifier ordering isn't flattened).</summary>
    private static IEnumerable<SearchResult> MergeByCommonness(List<List<(SearchResult Result, int PriorityBand, int Rarity)>> lists)
    {
        var heads = new int[lists.Count];
        while (true)
        {
            var best = -1;
            for (var k = 0; k < lists.Count; k++)
            {
                if (heads[k] >= lists[k].Count) continue;
                if (best < 0 || (lists[k][heads[k]].PriorityBand, lists[k][heads[k]].Rarity).CompareTo((lists[best][heads[best]].PriorityBand, lists[best][heads[best]].Rarity)) < 0)
                    best = k;
            }

            if (best < 0) yield break;
            yield return lists[best][heads[best]++].Result;
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
    /// best first: fewest conjugation steps, then <see cref="FormPriority.EffectiveBand"/>, then
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

        var best = new Dictionary<int, ((int Steps, int PriorityBand, int FewerExamples, int Rarity) Rank, Deinflection Source)>();
        foreach (var (text, source) in lookups)
        {
            foreach (var match in matchesByText[text])
            {
                if (!match.WordClasses.Any(wc => FitsInflectionType(wc, source.Type, text))) continue;

                var rank = (
                    source.Reasons.Count,
                    match.EffectiveBand,
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
        text.EnumerateRunes().Any(r => r.Value is >= 0x3040 and <= 0x30FF
            or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF or >= 0xFF66 and <= 0xFF9F
            or >= 0x20000 and <= 0x323AF);

    [GeneratedRegex(@"^[a-zA-Z']+$")]
    private static partial Regex RomajiOnly();

}
