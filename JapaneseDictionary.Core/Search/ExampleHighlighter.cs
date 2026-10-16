using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Finds where an entry occurs in an example sentence, including conjugated
/// occurrences (食べました, 飲んで, 勉強しています), by matching the sentence
/// against every surface form <see cref="Deinflector.GenerateForms"/> produces
/// from the entry's kanji spellings and readings. The longest match wins, so
/// 食べました highlights whole rather than as 食べ.
/// </summary>
public static class ExampleHighlighter
{
    /// <summary>Kana-only conjugated forms shorter than this are left out:
    /// for a word like いる, "いて" or "いた" would match inside unrelated words.
    /// Forms containing kanji are specific enough at any length (見て).</summary>
    private const int MinKanaOnlyFormLength = 3;

    /// <summary>All texts to look for in a sentence, longest first.</summary>
    public static IReadOnlyList<string> Candidates(DictionaryEntry entry, IReadOnlyList<WordClass> wordClasses)
    {
        var dictionaryForms = entry.KanjiForms.Select(k => k.Text).Concat(entry.Readings.Select(r => r.Text)).ToList();
        var candidates = new HashSet<string>(dictionaryForms, StringComparer.Ordinal);

        foreach (var wordClass in wordClasses.Distinct())
            foreach (var form in dictionaryForms)
                foreach (var surface in Deinflector.GenerateForms(form, wordClass).Keys)
                    if (surface.Any(IsKanji) || surface.Length >= MinKanaOnlyFormLength)
                        candidates.Add(surface);

        return candidates.OrderByDescending(c => c.Length).ThenBy(c => c, StringComparer.Ordinal).ToList();
    }

    /// <summary>The longest match, the earliest one among equally long matches
    /// (so 行く…ゆく highlights 行く, not whichever spelling sorts first), or null.</summary>
    public static (int Index, int Length)? Find(string sentence, IReadOnlyList<string> candidates)
    {
        (int Index, int Length)? best = null;
        foreach (var candidate in candidates)
        {
            if (best is { } b && candidate.Length < b.Length) break; // candidates are longest first
            var index = sentence.IndexOf(candidate, StringComparison.Ordinal);
            if (index >= 0 && (best == null || index < best.Value.Index))
                best = (index, candidate.Length);
        }

        return best;
    }

    private static bool IsKanji(char c) => c is >= '一' and <= '鿿';
}
