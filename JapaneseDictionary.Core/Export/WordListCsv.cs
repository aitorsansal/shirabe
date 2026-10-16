using System.Text;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Export;

/// <summary>
/// Builds a CSV of entries for import into Anki or a spreadsheet: one row per
/// entry with Word, Reading and Meanings columns. Senses are separated with
/// "; " and the glosses within a sense with ", ", so "to eat; to live on (e.g.
/// a salary), to live off" keeps the sense grouping readable in one cell.
/// </summary>
public static class WordListCsv
{
    private const string Header = "Word,Reading,Meanings";

    public static string Build(IEnumerable<DictionaryEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append("\r\n");
        foreach (var entry in entries)
        {
            var meanings = string.Join("; ", entry.Senses.Select(s => string.Join(", ", s.Glosses)).Where(m => m.Length > 0));
            sb.Append(Escape(entry.PrimaryText)).Append(',')
              .Append(Escape(entry.PrimaryReading)).Append(',')
              .Append(Escape(meanings)).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>UTF-8 with a BOM: Excel needs the BOM to read Japanese text
    /// correctly, and Anki ignores it.</summary>
    public static byte[] ToBytes(string csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];

    /// <summary>RFC 4180: quote fields containing a comma, quote or line break,
    /// doubling embedded quotes.</summary>
    internal static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
}
