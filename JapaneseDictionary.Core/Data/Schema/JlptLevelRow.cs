namespace JapaneseDictionary.Core.Data.Schema;

// Projection row for looking up the JLPT level of several entries. Not a table.
public sealed class JlptLevelRow
{
    public int EntryId { get; set; }
    public int Level { get; set; }
}
