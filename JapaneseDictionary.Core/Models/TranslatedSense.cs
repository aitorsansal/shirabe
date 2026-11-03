namespace JapaneseDictionary.Core.Models;

/// <summary>One sense of an entry in a non-English language. JMdict's senses in
/// other languages are separate elements, not aligned with the English ones,
/// so they form their own numbered list per language.</summary>
public sealed record TranslatedSense(string Lang, int Order, IReadOnlyList<string> Glosses);

/// <summary>A sentence's translation in one non-English language.</summary>
public sealed record SentenceTranslation(long SentenceId, string Lang, string Text);
