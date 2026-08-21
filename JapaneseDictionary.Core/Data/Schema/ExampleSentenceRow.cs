using SQLite;

namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>One Japanese/English sentence pair (Tatoeba sentence id).</summary>
public sealed class ExampleSentenceRow
{
    [PrimaryKey]
    public long Id { get; set; }

    public string Japanese { get; set; } = "";
    public string English { get; set; } = "";
}

/// <summary>Links an example sentence to a dictionary entry it illustrates
/// (from Tatoeba's Japanese Indices export).</summary>
public sealed class ExampleSentenceLinkRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long SentenceId { get; set; }

    [Indexed]
    public int EntryId { get; set; }
}
