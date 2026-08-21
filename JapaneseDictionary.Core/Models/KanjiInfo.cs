namespace JapaneseDictionary.Core.Models;

/// <summary>
/// One KANJIDIC2 entry: metadata about a single kanji character.
/// </summary>
public sealed record KanjiInfo(
    string Character,
    int StrokeCount,
    /// <summary>School grade the kanji is taught in Japan (1-6 = elementary,
    /// 8 = other Jouyou, 9/10 = Jinmeiyou), or null if ungraded.</summary>
    int? Grade,
    /// <summary>Old JLPT level (1-4, where 1 is hardest), or null if not assigned
    /// one. KANJIDIC2 only ships the pre-2010 4-level scale, not the current
    /// N1-N5 scale.</summary>
    int? JlptLevel,
    /// <summary>Frequency-of-use rank among common kanji (lower = more common),
    /// or null if outside the ranked set.</summary>
    int? FrequencyRank,
    /// <summary>Classic Kangxi radical number (1-214).</summary>
    int RadicalNumber,
    /// <summary>SKIP code (Halpern's "System of Kanji Indexing by Patterns"),
    /// e.g. "1-3-2", or null if not available.</summary>
    string? SkipCode,
    IReadOnlyList<string> OnReadings,
    IReadOnlyList<string> KunReadings,
    IReadOnlyList<string> Meanings);
