using System.Text.Encodings.Web;
using System.Text.Json;

using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Core.Export;

public static class CompanionExport
{
    public sealed record Row(DictionaryEntry Entry, int? JlptLevel, IReadOnlyList<TranslatedSense> Translations);
    public sealed record Result(byte[] Json, int Exported, int Skipped, bool Truncated);

    public const int MaxEntries = 2000;

    private const string DictionaryAttribution =
        "JMdict/EDICT © Electronic Dictionary Research and Development Group, CC BY-SA 4.0 (edrdg.org).";
    private const string JlptAttribution =
        " JLPT levels: Jonathan Waller (tanos.co.uk), CC BY, via stephenmk/yomitan-jlpt-vocab, CC BY-SA 4.0.";

    public static Result Build(IEnumerable<Row> rows, string? listName, Guid requestId)
    {
        var exported = new List<(Row Row, Dictionary<string, string[]> Glosses)>();
        var skipped = 0;
        var truncated = false;
        foreach (var row in rows)
        {
            if (exported.Count == MaxEntries)
            {
                truncated = true;
                break;
            }

            // MeaningGlosses hides romanizations of the reading, which is the only gloss of
            // loanwords like ラジオ "radio".
            var english = Meanings(row.Entry.MeaningGlosses);
            if (english.Length == 0) english = Meanings(row.Entry.Senses.SelectMany(s => s.Glosses));
            if (english.Length == 0 || !IsValidForm(row.Entry.PrimaryText) || !IsValidForm(row.Entry.PrimaryReading))
            {
                skipped++;
                continue;
            }

            var glosses = new Dictionary<string, string[]> { ["en"] = english };
            foreach (var language in row.Translations.GroupBy(t => t.Lang))
            {
                var code = TranslationLanguages.Find(language.Key)?.Iso1 ?? language.Key;
                if (code == "en") continue;
                var meanings = Meanings(language.OrderBy(t => t.Order).SelectMany(t => t.Glosses));
                if (meanings.Length > 0) glosses[code] = meanings;
            }
            exported.Add((row, glosses));
        }

        listName = listName?.Trim();
        if (string.IsNullOrEmpty(listName)) listName = null;
        else if (listName.Length > 60)
        {
            var length = char.IsHighSurrogate(listName[59]) && char.IsLowSurrogate(listName[60]) ? 59 : 60;
            listName = listName[..length];
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteString("requestId", requestId.ToString("D"));
            writer.WriteString("listName", listName);
            writer.WriteString("attribution", DictionaryAttribution + (exported.Any(e => e.Row.JlptLevel.HasValue) ? JlptAttribution : ""));
            writer.WriteStartArray("entries");
            foreach (var (row, glosses) in exported)
            {
                writer.WriteStartObject();
                writer.WriteString("source", "jmdict");
                writer.WriteNumber("entryId", row.Entry.Id);
                writer.WriteString("form", row.Entry.PrimaryText);
                writer.WriteString("reading", row.Entry.PrimaryReading);
                if (row.JlptLevel is { } level) writer.WriteNumber("jlpt", level);
                writer.WriteStartObject("glosses");
                foreach (var (language, meanings) in glosses)
                {
                    writer.WriteStartArray(language);
                    foreach (var meaning in meanings) writer.WriteStringValue(meaning);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return new Result(stream.ToArray(), exported.Count, skipped, truncated);
    }

    private static string[] Meanings(IEnumerable<string> glosses) =>
        glosses.Select(g => GlossNotes.WithoutOrigin(g.Trim())).Where(g => g.Length is > 0 and <= 80).Take(5).ToArray();

    private static bool IsValidForm(string text) =>
        text.Length <= 32 && !text.Any(c => c is '<' or '>' || char.IsControl(c));
}
