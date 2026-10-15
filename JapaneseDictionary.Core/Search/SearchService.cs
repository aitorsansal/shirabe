using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Data;

namespace JapaneseDictionary.Core.Search;

public enum SearchMatchKind
{
    ExactMatch,
    Prefix,
    Conjugated,
    Gloss,
}

public sealed record SearchResult(int EntryId, SearchMatchKind Kind, string? ConjugationForm);

public enum SearchMode { Japanese, English }

/// <summary>
/// Single entry point for dictionary search: kanji, kana (with romaji
/// auto-conversion), conjugated verb/adjective forms, and English meanings.
/// </summary>
public sealed partial class SearchService
{
    private readonly DictionaryRepository repo;

    /// <summary>Longest conjugation ending this deinflects (ませんでした, 6 chars) —
    /// bounds how far back from the query's end a dictionary-form stem is tried.</summary>
    private const int MaxEndingLength = 7;

    public SearchService(DictionaryRepository repo) => this.repo = repo;

    /// <summary>Minimum query length before an (unindexed, substring) gloss
    /// search is attempted — a 1-character English substring matches such a
    /// huge fraction of the ~440k gloss rows that it's a multi-second table
    /// scan for essentially no useful results.</summary>
    private const int MinGlossQueryLength = 2;

    /// <summary>How many reading candidates a single deinflection stem lookup
    /// considers. Ordered most-common-first, so this is generous headroom
    /// rather than a tight bound — measured ~30-70ms even for the busiest
    /// single-kana stems (き, し, た...) now that the candidate lookup no
    /// longer joins against the full entry table.</summary>
    private const int ReadingCandidateLimit = 3000;

    public IReadOnlyList<SearchResult> Search(string rawQuery, int limit = 50) =>
        Search(rawQuery, SearchMode.Japanese, limit);

    public IReadOnlyList<SearchResult> Search(string rawQuery, SearchMode mode, int limit = 50)
    {
        var query = rawQuery.Trim();
        if (query.Length == 0) return [];

        var results = new List<SearchResult>();
        var seen = new HashSet<int>();

        void AddAll(IEnumerable<int> ids, SearchMatchKind kind, string? form = null)
        {
            foreach (var id in ids)
            {
                if (seen.Add(id))
                    results.Add(new SearchResult(id, kind, form));
            }
        }

        var queryForms = BuildQueryForms(query);

        foreach (var form in queryForms)
            AddAll(repo.FindByExactText(form), SearchMatchKind.ExactMatch);

        if (mode == SearchMode.Japanese)
        {
            foreach (var form in queryForms.Where(IsKanaOnly))
                AddDeinflectedMatches(form, AddAll, limit);

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
            AddAll(repo.FindByGlossContains(query, limit - results.Count), SearchMatchKind.Gloss);

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

    private void AddDeinflectedMatches(string kanaQuery, Action<IEnumerable<int>, SearchMatchKind, string?> addAll, int limit)
    {
        for (var stemLength = kanaQuery.Length - 1; stemLength >= Math.Max(1, kanaQuery.Length - MaxEndingLength); stemLength--)
        {
            var stem = kanaQuery[..stemLength];
            var candidates = repo.FindReadingCandidates(stem, ReadingCandidateLimit);

            var matchedThisLength = false;
            foreach (var candidate in candidates)
            {
                foreach (var wordClass in candidate.WordClasses)
                {
                    var formName = Deinflector.TryGetFormName(kanaQuery, candidate.Reading, wordClass);
                    if (formName == null) continue;

                    addAll([candidate.EntryId], SearchMatchKind.Conjugated, formName);
                    matchedThisLength = true;
                }
            }

            // Once a stem length has produced matches, shorter (broader, more
            // expensive) stems are very unlikely to add correct ones — stop here.
            if (matchedThisLength) break;
        }
    }

    private static bool IsKanaOnly(string text) => KanaOnly().IsMatch(text);
    private static bool LooksLikeRomaji(string text) => RomajiOnly().IsMatch(text);

    [GeneratedRegex(@"^[぀-ゟ゠-ヿ]+$")]
    private static partial Regex KanaOnly();

    [GeneratedRegex(@"^[a-zA-Z']+$")]
    private static partial Regex RomajiOnly();

}
