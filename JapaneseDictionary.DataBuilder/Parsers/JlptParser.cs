using System.Text;
using System.Text.Json;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>
/// Reads the unofficial JLPT level lists (Jonathan Waller's, via stephenmk's
/// yomitan-jlpt-vocab for words and Bluskyo's JLPT_Vocabulary for kanji).
/// Levels are 5 (N5, easiest) to 1 (N1); an item listed at several levels keeps the easiest.
/// </summary>
public static class JlptParser
{
    /// <summary>JMdict entry id -> level, from <c>n1.csv</c>..<c>n5.csv</c> in <paramref name="directory"/>
    /// (columns <c>jmdict_seq,kana,kanji,waller_definition</c>). Ids not in
    /// <paramref name="knownEntryIds"/> are counted as unmatched and dropped.</summary>
    public static (Dictionary<int, int> Levels, Dictionary<int, int> UnmatchedPerLevel) ParseWords(
        string directory, IReadOnlySet<int> knownEntryIds)
    {
        var levels = new Dictionary<int, int>();
        var unmatched = new Dictionary<int, int>();

        // Hardest first, so a later (easier) list overwrites an earlier one.
        for (var level = 1; level <= 5; level++)
        {
            var path = Path.Combine(directory, $"n{level}.csv");
            var unmatchedHere = 0;
            foreach (var record in ReadCsv(path).Skip(1))
            {
                if (record.Count == 0 || !int.TryParse(record[0], out var id)) continue;
                if (!knownEntryIds.Contains(id)) { unmatchedHere++; continue; }
                levels[id] = level;
            }

            unmatched[level] = unmatchedHere;
        }

        return (levels, unmatched);
    }

    /// <summary>Kanji -> level from <c>JLPT_kanji_ALL.json</c> (a flat object of character to 1..5).</summary>
    public static Dictionary<string, int> ParseKanji(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var levels = new Dictionary<string, int>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var level = property.Value.GetInt32();
            if (level is >= 1 and <= 5) levels[property.Name] = level;
        }

        return levels;
    }

    /// <summary>Minimal RFC 4180 reader (one record per line): quoted fields may hold commas and doubled quotes.</summary>
    private static IEnumerable<List<string>> ReadCsv(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
                else field.Append(c);
            }

            fields.Add(field.ToString());
            yield return fields;
        }
    }
}
