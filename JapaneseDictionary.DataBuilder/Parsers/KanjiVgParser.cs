using System.Globalization;
using System.Xml.Linq;

namespace JapaneseDictionary.DataBuilder.Parsers;

/// <summary>Parses a KanjiVG combined XML export into per-character stroke path lists.</summary>
public static class KanjiVgParser
{
    /// <summary>Character -> ordered list of SVG path "d" strings, one per stroke, in drawing order.</summary>
    public static Dictionary<string, List<string>> Parse(string path)
    {
        var doc = XDocument.Load(path);

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var kanjiEl in doc.Root!.Elements("kanji"))
        {
            // id looks like "kvg:kanji_04e9c" - the hex suffix is the Unicode codepoint.
            var id = (string)kanjiEl.Attribute("id")!;
            var hex = id[(id.LastIndexOf('_') + 1)..];
            var codepoint = int.Parse(hex, NumberStyles.HexNumber);
            var character = char.ConvertFromUtf32(codepoint);

            // <path> elements can be nested inside multiple levels of <g> (used to
            // annotate radicals/components), but their document order is always
            // the actual stroke-drawing order regardless of nesting depth.
            var strokes = kanjiEl.Descendants("path").Select(p => (string)p.Attribute("d")!).ToList();
            if (strokes.Count == 0) continue;

            result[character] = strokes;
        }

        return result;
    }
}
