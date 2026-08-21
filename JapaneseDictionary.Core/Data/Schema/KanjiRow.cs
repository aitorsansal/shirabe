using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>SQLite row for one KANJIDIC2 character entry.</summary>
public sealed class KanjiRow
{
    [PrimaryKey]
    public string Character { get; set; } = "";

    public int StrokeCount { get; set; }
    public int? Grade { get; set; }
    public int? JlptLevel { get; set; }
    public int? FrequencyRank { get; set; }
    public int RadicalNumber { get; set; }
    public string? SkipCode { get; set; }

    /// <summary>Semicolon-joined on'yomi readings, in katakana.</summary>
    public string OnReadingsJoined { get; set; } = "";

    /// <summary>Semicolon-joined kun'yomi readings, in hiragana (may include an
    /// okurigana split marked with a dot, e.g. "た.べる", as KANJIDIC2 stores it).</summary>
    public string KunReadingsJoined { get; set; } = "";

    /// <summary>Semicolon-joined English meanings.</summary>
    public string MeaningsJoined { get; set; } = "";
}

/// <summary>One stroke of a kanji's stroke-order diagram, from KanjiVG. One row
/// per stroke, in drawing order.</summary>
public sealed class KanjiStrokeRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Character { get; set; } = "";

    /// <summary>1-based drawing order.</summary>
    public int StrokeIndex { get; set; }

    /// <summary>The stroke's SVG path "d" attribute, in KanjiVG's own coordinate
    /// space (a 109x109 viewBox).</summary>
    public string SvgPath { get; set; } = "";
}
