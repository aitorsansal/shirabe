using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>
/// Parses a JMdict XML file (the EDRDG-published Japanese/English dictionary
/// data) into <see cref="DictionaryEntry"/> records.
/// </summary>
public static partial class JmdictParser
{
    private static readonly HashSet<string> CommonPriorityMarkers =
        new(StringComparer.Ordinal) { "news1", "ichi1", "spec1", "spec2", "gai1" };

    private static readonly XName XmlLang = XNamespace.Xml + "lang";

    /// <summary>The parsed dictionary: the English entries exactly as before, plus
    /// each entry's meanings in the other supported languages (see
    /// <see cref="TranslationLanguages"/>), keyed by entry id.</summary>
    public sealed record Result(
        List<DictionaryEntry> Entries,
        Dictionary<int, List<TranslatedSense>> TranslatedSenses);

    public static Result Parse(string path)
    {
        var codeByText = ExtractEntityCodesByText(path);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            MaxCharactersFromEntities = 0, // unlimited: JMdict's own entities are reused tens of thousands of times, well past the default expansion-bomb guard
        };
        using var reader = XmlReader.Create(path, settings);
        var doc = XDocument.Load(reader);

        var entries = new List<DictionaryEntry>();
        var translated = new Dictionary<int, List<TranslatedSense>>();
        var wantedLangs = TranslationLanguages.All.Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var entryEl in doc.Root!.Elements("entry"))
        {
            var id = int.Parse(entryEl.Element("ent_seq")!.Value);

            var kanjiForms = entryEl.Elements("k_ele")
                .Select(k => new KanjiForm(
                    k.Element("keb")!.Value,
                    k.Elements("ke_pri").Any(IsCommonPriority),
                    FormPriority.FromMarkers(k.Elements("ke_pri").Select(p => p.Value))))
                .ToList();

            var readingForms = entryEl.Elements("r_ele")
                .Select(r =>
                {
                    var restrictedTo = r.Elements("re_restr").Select(x => x.Value).ToList();
                    return new ReadingForm(
                        r.Element("reb")!.Value,
                        r.Elements("re_pri").Any(IsCommonPriority),
                        restrictedTo.Count == 0,
                        restrictedTo,
                        FormPriority.FromMarkers(r.Elements("re_pri").Select(p => p.Value)));
                })
                .ToList();

            if (readingForms.Count == 0) continue; // guard: every real entry has one per the DTD

            var senses = entryEl.Elements("sense")
                .Select(senseEl =>
                {
                    var posCodes = ResolveCodes(senseEl, "pos", codeByText);
                    var fieldCodes = ResolveCodes(senseEl, "field", codeByText);
                    var miscCodes = ResolveCodes(senseEl, "misc", codeByText);
                    var glosses = senseEl.Elements("gloss").Where(IsEnglishGloss).Select(g => g.Value).ToList();
                    var wordClasses = posCodes
                        .Select(WordClassMap.FromJmdictTag)
                        .Where(wc => wc != WordClass.Other)
                        .Distinct()
                        .ToList();

                    return new WordSense(wordClasses, posCodes, glosses, fieldCodes, miscCodes);
                })
                .Where(s => s.Glosses.Count > 0)
                .ToList();

            if (senses.Count == 0) continue;

            entries.Add(new DictionaryEntry(id, kanjiForms, readingForms, senses));

            // Other languages come as their own <sense> elements, not aligned with
            // the English ones, so each language gets an independent numbering.
            var perLangOrder = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var senseEl in entryEl.Elements("sense"))
            {
                foreach (var byLang in senseEl.Elements("gloss")
                    .Select(g => (Lang: g.Attribute(XmlLang)?.Value ?? "eng", Text: g.Value))
                    .Where(g => wantedLangs.Contains(g.Lang))
                    .GroupBy(g => g.Lang))
                {
                    var order = perLangOrder[byLang.Key] = perLangOrder.GetValueOrDefault(byLang.Key) + 1;
                    if (!translated.TryGetValue(id, out var list)) translated[id] = list = [];
                    list.Add(new TranslatedSense(byLang.Key, order, byLang.Select(g => g.Text).ToList()));
                }
            }
        }

        return new Result(entries, translated);
    }

    /// <summary>Glosses with no xml:lang are English by the DTD's default.</summary>
    private static bool IsEnglishGloss(XElement gloss) =>
        gloss.Attribute(XmlLang) is not { } lang || lang.Value == "eng";

    private static List<string> ResolveCodes(XElement senseEl, string elementName, Dictionary<string, string> codeByText) =>
        senseEl.Elements(elementName)
            .Select(e => codeByText.GetValueOrDefault(e.Value, e.Value))
            .ToList();

    private static bool IsCommonPriority(XElement priorityElement) =>
        CommonPriorityMarkers.Contains(priorityElement.Value);

    /// <summary>
    /// JMdict tags (pos/field/misc/dial) are stored as internal DTD entities,
    /// e.g. &lt;!ENTITY v1 "Ichidan verb"&gt;, so the XML reader resolves
    /// &amp;v1; to the full description text rather than the short code. This
    /// reads the DOCTYPE's own entity declarations to build a reverse map
    /// (description text -&gt; short code) so we can recover "v1" instead of
    /// storing "Ichidan verb" everywhere.
    /// </summary>
    private static Dictionary<string, string> ExtractEntityCodesByText(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("<!ENTITY", StringComparison.Ordinal))
            {
                var match = EntityDeclaration().Match(line);
                if (match.Success)
                    map[match.Groups["text"].Value] = match.Groups["code"].Value;
            }
            else if (line.Contains("]>", StringComparison.Ordinal))
            {
                break; // end of the DOCTYPE internal subset; no entities appear after this
            }
        }

        return map;
    }

    [GeneratedRegex("""^<!ENTITY\s+(?<code>[\w-]+)\s+"(?<text>[^"]*)"\s*>""")]
    private static partial Regex EntityDeclaration();
}
