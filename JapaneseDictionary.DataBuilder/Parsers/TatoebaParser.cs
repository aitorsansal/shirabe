using System.Text.RegularExpressions;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>
/// Parses Tatoeba's Japanese Indices export (jpn_indices.csv) plus the raw
/// sentences export (sentences.csv) into <see cref="ExampleSentence"/>
/// records linked to JMdict entries.
///
/// jpn_indices.csv columns: japanese_sentence_id, english_sentence_id,
/// index_line. The index line lists the sentence's words each optionally
/// decorated with an alternate reading in {}, an alternate spelling in (),
/// a sense number in [], an explicit JMdict entry id as (#nnnnnnn), or a
/// trailing ~ marking the word as auxiliary. See
/// https://tatoeba.org/en/downloads for the format.
/// </summary>
public static partial class TatoebaParser
{
    public static List<ExampleSentence> Parse(string jpnIndicesPath, string sentencesPath, IReadOnlyList<DictionaryEntry> dictionary)
    {
        var headwordIndex = BuildHeadwordIndex(dictionary);

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

            var linkedIds = ResolveLinkedEntryIds(line.IndexText, headwordIndex);
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

    private static List<int> ResolveLinkedEntryIds(string indexText, Dictionary<string, List<int>> headwordIndex)
    {
        var ids = new HashSet<int>();

        foreach (Match m in ExplicitEntryId().Matches(indexText))
            ids.Add(int.Parse(m.Groups[1].Value));

        foreach (var token in indexText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains("(#", StringComparison.Ordinal)) continue; // already handled above

            var headword = BaseHeadword(token);
            if (headword.Length == 0) continue;
            if (headwordIndex.TryGetValue(headword, out var candidates) && candidates.Count == 1)
                ids.Add(candidates[0]);
        }

        return ids.ToList();
    }

    private static string BaseHeadword(string token)
    {
        var end = token.IndexOfAny(['(', '{', '[', '~']);
        return end < 0 ? token : token[..end];
    }

    [GeneratedRegex(@"\(#(\d+)\)")]
    private static partial Regex ExplicitEntryId();

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
