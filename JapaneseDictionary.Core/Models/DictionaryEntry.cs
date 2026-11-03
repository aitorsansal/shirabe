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
    /// spelling if there is one, otherwise the most common reading. Words
    /// usually written in kana (<see cref="IsUsuallyKana"/>) lead with the
    /// reading instead; their kanji spellings still show as other forms.</summary>
    public string PrimaryText =>
        (IsUsuallyKana ? null : KanjiForms.FirstOrDefault(k => k.IsCommon)?.Text ?? KanjiForms.FirstOrDefault()?.Text)
        ?? PrimaryReading;

    /// <summary>True when the first sense is tagged "uk" (usually written using
    /// kana alone) and no kanji spelling is common, as for ながら (乍ら), しまう
    /// (仕舞う), ちょっと (一寸) or ありがとう (有難う). Words whose kanji spelling is
    /// common too (分かる, 出来る, 沢山) keep it as the headline, since learners
    /// meet it in print and the kanji is worth learning.</summary>
    public bool IsUsuallyKana =>
        Senses.Count > 0
        && Senses[0].MiscTags.Contains("uk")
        && !KanjiForms.Any(k => k.IsCommon);

    /// <summary>The reading to show alongside <see cref="PrimaryText"/>.</summary>
    public string PrimaryReading =>
        Readings.FirstOrDefault(r => r.IsCommon)?.Text ?? Readings[0].Text;

    /// <summary>Every gloss in sense order, minus ones that only spell a
    /// reading in romaji (お食い初め's "okuizome"). For one-line previews
    /// (search rows, related words); the full meaning list shows everything.</summary>
    public IEnumerable<string> MeaningGlosses =>
        Senses.SelectMany(s => s.Glosses)
            .Where(g => !Readings.Any(r => Search.RomajiConverter.IsRomanizationOf(g, r.Text)));
}

/// <summary>A kanji spelling of an entry (JMdict k_ele).</summary>
/// <param name="Priority">See <see cref="FormPriority"/>; lower is more frequent.</param>
public sealed record KanjiForm(string Text, bool IsCommon, int Priority = FormPriority.None);

/// <summary>A reading of an entry (JMdict r_ele).</summary>
public sealed record ReadingForm(
    string Text,
    bool IsCommon,
    /// <summary>False if this reading only applies to some of the entry's kanji
    /// forms (JMdict re_restr) rather than all of them.</summary>
    bool AppliesToAllKanjiForms,
    IReadOnlyList<string> RestrictedToKanjiForms,
    /// <summary>See <see cref="FormPriority"/>; lower is more frequent.</summary>
    int Priority = FormPriority.None);

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
