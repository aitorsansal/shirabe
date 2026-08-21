namespace JapaneseDictionary.Core.Models;

/// <summary>
/// One JMdict entry: a word/expression with its kanji spellings, readings,
/// and senses (groups of English glosses that share a meaning).
/// </summary>
public sealed record DictionaryEntry(
    int Id,
    IReadOnlyList<KanjiForm> KanjiForms,
    IReadOnlyList<ReadingForm> Readings,
    IReadOnlyList<WordSense> Senses)
{
    /// <summary>The form to show as the entry's headline: the most common kanji
    /// spelling if there is one, otherwise the most common reading.</summary>
    public string PrimaryText =>
        KanjiForms.FirstOrDefault(k => k.IsCommon)?.Text
        ?? KanjiForms.FirstOrDefault()?.Text
        ?? Readings.FirstOrDefault(r => r.IsCommon)?.Text
        ?? Readings[0].Text;

    /// <summary>The reading to show alongside <see cref="PrimaryText"/>.</summary>
    public string PrimaryReading =>
        Readings.FirstOrDefault(r => r.IsCommon)?.Text ?? Readings[0].Text;
}

/// <summary>A kanji spelling of an entry (JMdict k_ele).</summary>
public sealed record KanjiForm(string Text, bool IsCommon);

/// <summary>A reading of an entry (JMdict r_ele).</summary>
public sealed record ReadingForm(
    string Text,
    bool IsCommon,
    /// <summary>False if this reading only applies to some of the entry's kanji
    /// forms (JMdict re_restr) rather than all of them.</summary>
    bool AppliesToAllKanjiForms,
    IReadOnlyList<string> RestrictedToKanjiForms);

/// <summary>One sense (meaning group) of an entry (JMdict sense).</summary>
public sealed record WordSense(
    /// <summary>Conjugation classes this sense's part-of-speech implies, if any
    /// (a sense can be tagged as more than one, e.g. both transitive and
    /// intransitive godan). Empty for non-conjugating parts of speech (nouns,
    /// particles, etc.).</summary>
    IReadOnlyList<WordClass> WordClasses,
    /// <summary>Raw part-of-speech tags as JMdict stores them (e.g. "n", "v1", "adj-i").</summary>
    IReadOnlyList<string> PartOfSpeechTags,
    IReadOnlyList<string> Glosses,
    /// <summary>Subject-matter field tags, e.g. "comp" (computing), "med" (medicine).</summary>
    IReadOnlyList<string> Fields,
    /// <summary>Usage/register tags, e.g. "col" (colloquial), "arch" (archaic).</summary>
    IReadOnlyList<string> MiscTags);
