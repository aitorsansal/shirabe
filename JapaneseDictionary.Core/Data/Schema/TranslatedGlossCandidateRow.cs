namespace JapaneseDictionary.Core.Data.Schema;

/// <summary>Projection row for translated-gloss search: one non-English sense
/// whose joined glosses passed the SQL pre-filter, with the entry-level fields
/// the ranker needs. Not a table.</summary>
public sealed class TranslatedGlossCandidateRow
{
    public int EntryId { get; set; }
    public int SenseOrder { get; set; }
    public string GlossesJoined { get; set; } = "";
    public bool IsCommon { get; set; }
    public int Priority { get; set; } = Models.FormPriority.None;
    public string PrimaryText { get; set; } = "";
    public int? JlptLevel { get; set; }
}
