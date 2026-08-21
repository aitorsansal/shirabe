using System.Xml.Linq;

using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>Parses a KANJIDIC2 XML file (the EDRDG-published kanji metadata) into <see cref="KanjiInfo"/> records.</summary>
public static class Kanjidic2Parser
{
    public static List<KanjiInfo> Parse(string path)
    {
        var doc = XDocument.Load(path);

        return doc.Root!.Elements("character").Select(charEl =>
        {
            var literal = charEl.Element("literal")!.Value;

            var misc = charEl.Element("misc")!;
            var strokeCount = int.Parse(misc.Element("stroke_count")!.Value);
            var grade = (int?)misc.Element("grade");
            var jlpt = (int?)misc.Element("jlpt");
            var freq = (int?)misc.Element("freq");

            var radicalNumber = int.Parse(charEl.Element("radical")!
                .Elements("rad_value")
                .First(r => (string)r.Attribute("rad_type")! == "classical")
                .Value);

            var skip = charEl.Element("query_code")?
                .Elements("q_code")
                .FirstOrDefault(q => (string)q.Attribute("qc_type")! == "skip")?
                .Value;

            var rmGroup = charEl.Element("reading_meaning")?.Element("rmgroup");
            var onReadings = rmGroup?.Elements("reading")
                .Where(r => (string)r.Attribute("r_type")! == "ja_on")
                .Select(r => r.Value).ToList() ?? [];
            var kunReadings = rmGroup?.Elements("reading")
                .Where(r => (string)r.Attribute("r_type")! == "ja_kun")
                .Select(r => r.Value).ToList() ?? [];
            // English meanings only: entries without m_lang are English per the KANJIDIC2 DTD.
            var meanings = rmGroup?.Elements("meaning")
                .Where(m => m.Attribute("m_lang") is null)
                .Select(m => m.Value).ToList() ?? [];

            return new KanjiInfo(literal, strokeCount, grade, jlpt, freq, radicalNumber, skip, onReadings, kunReadings, meanings);
        }).ToList();
    }
}
