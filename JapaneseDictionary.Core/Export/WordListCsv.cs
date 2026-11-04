using System.Text;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Export;

/// <summary>
/// Builds a CSV of entries for import into Anki 2.1.54+ / AnkiDroid. The leading
/// "#key:value" lines are Anki's file-header directives: they set the separator
/// and HTML mode, name the columns and say which hold the tags and the note GUID,
/// so nothing is mapped by hand and no header row is imported as a card.
/// Word and Back come first because Anki's Basic note type maps columns in order
/// to Front/Back; Reading and Meanings are plain columns for custom note types.
/// The GUID (<c>shirabe-{JMdict id}</c>) is stable per entry, so re-importing an
/// edited list updates the existing notes instead of duplicating them. HTML mode
/// is on so Back can use line breaks, which means every text piece is escaped.
/// </summary>
public static class WordListCsv
{
    /// <param name="Entry">The word to export.</param>
    /// <param name="JlptLevel">N-level 1 to 5, or null when unlisted.</param>
    /// <param name="Translations">Translated senses for the languages the user selected.</param>
    public sealed record Row(DictionaryEntry Entry, int? JlptLevel, IReadOnlyList<TranslatedSense> Translations);

    private const string Header =
        "#separator:Comma\r\n#html:true\r\n#columns:Word,Back,Reading,Meanings,Tags,GUID\r\n#tags column:5\r\n#guid column:6\r\n";

    public static string Build(IEnumerable<Row> rows, string listName)
    {
        var listTag = string.Join('_', listName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var baseTags = listTag.Length > 0 ? $"shirabe {listTag}" : "shirabe";

        var sb = new StringBuilder(Header);
        foreach (var row in rows)
        {
            var entry = row.Entry;
            var meanings = string.Join("; ", entry.Senses.Select(s => string.Join(", ", s.Glosses)).Where(m => m.Length > 0));
            var tags = row.JlptLevel is { } level ? $"{baseTags} JLPT_N{level}" : baseTags;

            sb.Append(Escape(H(entry.PrimaryText))).Append(',')
              .Append(Escape(BuildBack(row))).Append(',')
              .Append(Escape(H(entry.PrimaryReading))).Append(',')
              .Append(Escape(H(meanings))).Append(',')
              .Append(Escape(tags)).Append(',')
              .Append(Escape($"shirabe-{entry.Id}")).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>UTF-8 without a BOM, which Anki reads by default. A BOM would put
    /// U+FEFF in front of the first "#separator" directive, and nothing in Anki's
    /// manual says it skips one before parsing headers. (Excel wanted the BOM, but
    /// the # lines already make this an Anki file rather than a spreadsheet.)</summary>
    public static byte[] ToBytes(string csv) => Encoding.UTF8.GetBytes(csv);

    /// <summary>RFC 4180: quote fields containing a comma, quote or line break,
    /// doubling embedded quotes.</summary>
    internal static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    /// <summary>Only &amp;, &lt; and &gt; need escaping for Anki's HTML mode.</summary>
    private static string H(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string BuildBack(Row row)
    {
        var lines = new List<string> { H(row.Entry.PrimaryReading) };

        var senses = row.Entry.Senses.Select(s => string.Join(", ", s.Glosses)).Where(m => m.Length > 0).ToList();
        for (var i = 0; i < senses.Count; i++)
            lines.Add(senses.Count == 1 ? H(senses[0]) : $"{i + 1}. {H(senses[i])}");

        // All order, not input order, so the lines are stable whatever the caller passes.
        foreach (var language in TranslationLanguages.All)
        {
            var translated = row.Translations.Where(t => t.Lang == language.Code).OrderBy(t => t.Order)
                .Select(t => string.Join(", ", t.Glosses)).Where(m => m.Length > 0).ToList();
            if (translated.Count > 0)
                lines.Add($"{H(language.Endonym)}: {H(string.Join("; ", translated))}");
        }

        return string.Join("<br>", lines);
    }
}
