namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>Projection row for gloss search: one gloss that contains the
/// query, with the entry-level fields the ranker needs. Not a table.</summary>
public sealed class GlossCandidateRow
{
    public int EntryId { get; set; }
    public string GlossText { get; set; } = "";
    public int SenseOrder { get; set; }
    public int GlossOrder { get; set; }
    public bool IsCommon { get; set; }
    public string PrimaryText { get; set; } = "";
}
