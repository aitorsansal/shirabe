using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>One non-English sense (see <see cref="Models.TranslatedSense"/>), its
/// glosses joined with <see cref="GlossSeparator"/>. One row per sense rather than
/// per gloss: German alone has 337k glosses, and per-row ids and index entries made
/// the table ~7 MB larger on disk for no benefit, since glosses are only ever read
/// together.</summary>
public sealed class TranslatedSenseRow
{
    /// <summary>U+001F (unit separator): never part of JMdict text.</summary>
    public const char GlossSeparator = '';

    [Indexed]
    public int EntryId { get; set; }

    /// <summary>ISO 639-2/B code as in JMdict: "spa", "fre", "ger".</summary>
    public string Lang { get; set; } = "";

    /// <summary>1-based position among the entry's senses in this language.</summary>
    public int SenseOrder { get; set; }

    public string GlossesJoined { get; set; } = "";
}

/// <summary>A direct Tatoeba translation of a Japanese example sentence.</summary>
public sealed class SentenceTranslationRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>The Japanese sentence id, as in <see cref="ExampleSentenceRow.Id"/>.</summary>
    [Indexed]
    public long SentenceId { get; set; }

    public string Lang { get; set; } = "";
    public string Text { get; set; } = "";
}
