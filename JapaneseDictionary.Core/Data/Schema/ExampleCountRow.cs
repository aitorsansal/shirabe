namespace JapaneseDictionary.Core.Data.Schema;

// Projection row for counting the example sentences linked to an entry. Not a table.
public sealed class ExampleCountRow
{
    public int EntryId { get; set; }
    public int Count { get; set; }
}
