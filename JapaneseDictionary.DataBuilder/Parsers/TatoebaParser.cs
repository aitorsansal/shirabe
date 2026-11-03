using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>
/// Parses Tatoeba's Japanese Indices export (jpn_indices.csv) plus the raw
/// sentences export (sentences.csv) into <see cref="ExampleSentence"/>
/// records linked to JMdict entries.
///
/// jpn_indices.csv columns: japanese_sentence_id, english_sentence_id,
/// index_line. The index line lists the sentence's words in dictionary form,
/// each optionally decorated with its reading in () (紙(かみ)), an explicit
/// JMdict entry id as (#nnnnnnn), a sense number in [] ([01]), the form as it
/// appears in the sentence in {} (包む{包んで}), and a trailing ~ marking the
/// sentence as a checked, good example of that word. See
/// https://tatoeba.org/en/downloads for the format.
/// </summary>
public static partial class TatoebaParser
{
    public static List<ExampleSentence> Parse(string jpnIndicesPath, string sentencesPath, IReadOnlyList<DictionaryEntry> dictionary)
    {
        var headwordIndex = BuildHeadwordIndex(dictionary);
        var readingsByEntry = dictionary.ToDictionary(e => e.Id, e => e.Readings.Select(r => r.Text).ToHashSet(StringComparer.Ordinal));

        var indexLines = File.ReadAllLines(jpnIndicesPath)
            .Select(ParseIndexLine)
            .ToList();

        var neededSentenceIds = new HashSet<long>();
        foreach (var line in indexLines)
        {
            neededSentenceIds.Add(line.JapaneseId);
            neededSentenceIds.Add(line.EnglishId);
        }

        var textById = LoadSentenceText(sentencesPath, neededSentenceIds);

        var results = new List<ExampleSentence>(indexLines.Count);
        foreach (var line in indexLines)
        {
            if (!textById.TryGetValue(line.JapaneseId, out var japanese)) continue;
            if (!textById.TryGetValue(line.EnglishId, out var english)) continue;

            var linkedIds = ResolveLinkedEntryIds(line.IndexText, headwordIndex, readingsByEntry);
            results.Add(new ExampleSentence(line.JapaneseId, japanese, english, linkedIds));
        }

        return results;
    }

    private readonly record struct IndexLine(long JapaneseId, long EnglishId, string IndexText);

    private static IndexLine ParseIndexLine(string line)
    {
        var parts = line.Split('\t', 3);
        return new IndexLine(long.Parse(parts[0]), long.Parse(parts[1]), parts[2]);
    }

    /// <summary>Maps every kanji form and reading text, across all entries, to
    /// the entry id(s) that use it — used for best-effort linking of index
    /// words that don't carry an explicit (#id) marker.</summary>
    private static Dictionary<string, List<int>> BuildHeadwordIndex(IReadOnlyList<DictionaryEntry> dictionary)
    {
        var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var entry in dictionary)
        {
            foreach (var form in entry.KanjiForms.Select(k => k.Text).Concat(entry.Readings.Select(r => r.Text)))
            {
                if (!index.TryGetValue(form, out var ids))
                    index[form] = ids = [];
                ids.Add(entry.Id);
            }
        }

        return index;
    }

    /// <summary>Entry ids the index words refer to: explicit (#id) markers, plus every
    /// word whose headword names exactly one entry, or exactly one once narrowed
    /// to the entries that have its () reading. Without that narrowing, every
    /// headword shared by homographs (紙, 髪, 神 and 上 all have several entries)
    /// was dropped, leaving thousands of common words without examples.</summary>
    private static List<int> ResolveLinkedEntryIds(
        string indexText,
        Dictionary<string, List<int>> headwordIndex,
        IReadOnlyDictionary<int, HashSet<string>> readingsByEntry)
    {
        var ids = new HashSet<int>();

        foreach (Match m in ExplicitEntryId().Matches(indexText))
            ids.Add(int.Parse(m.Groups[1].Value));

        foreach (var token in indexText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains("(#", StringComparison.Ordinal)) continue; // already handled above

            var (headword, reading) = SplitToken(token);
            if (headword.Length == 0 || !headwordIndex.TryGetValue(headword, out var candidates)) continue;

            if (candidates.Count == 1)
            {
                ids.Add(candidates[0]);
            }
            else if (reading != null)
            {
                var withReading = candidates.Where(id => readingsByEntry.TryGetValue(id, out var readings) && readings.Contains(reading)).Distinct().ToList();
                if (withReading.Count == 1)
                    ids.Add(withReading[0]);
            }
        }

        return ids.ToList();
    }

    /// <summary>The dictionary-form headword and, when given, the () reading:
    /// "包む(つつむ)[01]{包んで}" gives (包む, つつむ), "です" gives (です, null).</summary>
    private static (string Headword, string? Reading) SplitToken(string token)
    {
        var end = token.IndexOfAny(['(', '{', '[', '~']);
        if (end < 0) return (token, null);

        var headword = token[..end];
        if (token[end] != '(') return (headword, null);

        var close = token.IndexOf(')', end + 1);
        return close < 0 ? (headword, null) : (headword, token[(end + 1)..close]);
    }

    [GeneratedRegex(@"\(#(\d+)\)")]
    private static partial Regex ExplicitEntryId();

    /// <summary>
    /// Direct translations of the given Japanese sentences into the supported
    /// non-English languages. links.csv (id, tab, id; ~450 MB) is streamed once to
    /// find the sentences linked to ours, then sentences.csv once for only those
    /// candidates' texts; with several translations in one language the lowest
    /// sentence id wins.
    /// </summary>
    public static List<SentenceTranslation> ParseTranslations(string linksPath, string sentencesPath, IEnumerable<long> japaneseIds)
    {
        var wanted = japaneseIds.ToHashSet();

        // candidate sentence id -> the Japanese ids it is linked to
        var candidates = new Dictionary<long, List<long>>();
        using (var reader = new StreamReader(linksPath))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var tab = line.IndexOf('\t');
                if (tab < 0) continue;
                if (!long.TryParse(line.AsSpan(0, tab), out var a) || !long.TryParse(line.AsSpan(tab + 1), out var b)) continue;

                if (wanted.Contains(a)) AddCandidate(candidates, b, a);
                if (wanted.Contains(b)) AddCandidate(candidates, a, b);
            }
        }

        var tatoebaToCode = TranslationLanguages.All.ToDictionary(l => l.TatoebaCode, l => l.Code, StringComparer.Ordinal);
        var best = new Dictionary<(long Japanese, string Lang), (long Id, string Text)>();
        using (var reader = new StreamReader(sentencesPath))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var firstTab = line.IndexOf('\t');
                if (firstTab < 0) continue;
                var secondTab = line.IndexOf('\t', firstTab + 1);
                if (secondTab < 0) continue;
                if (!tatoebaToCode.TryGetValue(line.Substring(firstTab + 1, secondTab - firstTab - 1), out var code)) continue;
                if (!long.TryParse(line.AsSpan(0, firstTab), out var id) || !candidates.TryGetValue(id, out var linkedJapanese)) continue;

                var text = line[(secondTab + 1)..];
                foreach (var jp in linkedJapanese)
                {
                    var key = (jp, code);
                    if (!best.TryGetValue(key, out var current) || id < current.Id)
                        best[key] = (id, text);
                }
            }
        }

        return best
            .OrderBy(kv => kv.Key.Japanese).ThenBy(kv => kv.Key.Lang, StringComparer.Ordinal)
            .Select(kv => new SentenceTranslation(kv.Key.Japanese, kv.Key.Lang, kv.Value.Text))
            .ToList();
    }

    private static void AddCandidate(Dictionary<long, List<long>> candidates, long candidate, long japanese)
    {
        if (!candidates.TryGetValue(candidate, out var list)) candidates[candidate] = list = [];
        list.Add(japanese);
    }

    private static Dictionary<long, string> LoadSentenceText(string sentencesPath, HashSet<long> neededIds)
    {
        var result = new Dictionary<long, string>(neededIds.Count);
        using var reader = new StreamReader(sentencesPath);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var firstTab = line.IndexOf('\t');
            if (firstTab < 0) continue;
            if (!long.TryParse(line.AsSpan(0, firstTab), out var id) || !neededIds.Contains(id)) continue;

            var secondTab = line.IndexOf('\t', firstTab + 1);
            if (secondTab < 0) continue;

            result[id] = line[(secondTab + 1)..];
            if (result.Count == neededIds.Count) break;
        }

        return result;
    }
}
