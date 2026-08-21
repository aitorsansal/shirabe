using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>One sense (meaning group) of an entry.</summary>
public sealed class SenseRow
{
    /// <summary>Explicitly assigned by the writer (not auto-increment) so that
    /// <see cref="GlossRow.SenseId"/> can be known before the row is inserted,
    /// letting all senses and all glosses each be written in one bulk insert.</summary>
    [PrimaryKey]
    public int Id { get; set; }

    [Indexed]
    public int EntryId { get; set; }

    /// <summary>Display order among the entry's senses (1-based). Named
    /// SenseOrder rather than Order since ORDER is a reserved SQL keyword.</summary>
    public int SenseOrder { get; set; }

    /// <summary>Semicolon-joined raw JMdict part-of-speech tags, e.g. "v1;n".</summary>
    public string PartOfSpeechTagsJoined { get; set; } = "";

    /// <summary>Semicolon-joined subject-field tags, e.g. "comp;med".</summary>
    public string FieldsJoined { get; set; } = "";

    /// <summary>Semicolon-joined usage/register tags, e.g. "col;arch".</summary>
    public string MiscTagsJoined { get; set; } = "";
}

/// <summary>One English gloss within a sense.</summary>
public sealed class GlossRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SenseId { get; set; }

    /// <summary>Display order among the sense's glosses (1-based). Named
    /// GlossOrder rather than Order since ORDER is a reserved SQL keyword.</summary>
    public int GlossOrder { get; set; }

    public string Text { get; set; } = "";
}
