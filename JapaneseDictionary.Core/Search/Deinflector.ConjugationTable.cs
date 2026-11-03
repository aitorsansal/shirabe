using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

/// <summary>Irregular verb subclasses (JMdict part-of-speech tags that
/// <see cref="WordClassMap"/> folds into a regular <see cref="WordClass"/>) whose
/// conjugation table differs from the class's regular one. Search ignores
/// these: it keeps the regular rules.</summary>
public enum ConjugationVariant
{
    None,
    /// <summary>v5r-i: ある.</summary>
    GodanAruIrregular,
    /// <summary>v5aru: くださる, いらっしゃる, なさる, おっしゃる, ござる.</summary>
    GodanAru,
    /// <summary>v5u-s: 問う, 請う, 乞う.</summary>
    GodanUArchaic,
    /// <summary>vs-s: 愛する, 察する, 訳する.</summary>
    SuruS,
    /// <summary>vz: 信ずる, 感ずる.</summary>
    Zuru,
    /// <summary>v1-s: くれる.</summary>
    IchidanKureru,
}

public static partial class Deinflector
{
    /// <summary>The first irregular subclass among raw JMdict part-of-speech
    /// <paramref name="tags"/>, or <see cref="ConjugationVariant.None"/>.</summary>
    public static ConjugationVariant VariantFromTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            var variant = tag switch
            {
                "v5r-i" => ConjugationVariant.GodanAruIrregular,
                "v5aru" => ConjugationVariant.GodanAru,
                "v5u-s" => ConjugationVariant.GodanUArchaic,
                "vs-s" => ConjugationVariant.SuruS,
                "vz" => ConjugationVariant.Zuru,
                "v1-s" => ConjugationVariant.IchidanKureru,
                _ => ConjugationVariant.None,
            };
            if (variant != ConjugationVariant.None) return variant;
        }

        return ConjugationVariant.None;
    }

    /// <summary>The variant among the entry's raw tags that map to
    /// <paramref name="primaryClass"/> (first matching tag wins). Goes by the tags
    /// rather than <see cref="WordSense.WordClasses"/>, which entries loaded from
    /// the database leave empty.</summary>
    public static ConjugationVariant VariantFor(DictionaryEntry entry, WordClass primaryClass) =>
        VariantFromTags(entry.Senses
            .SelectMany(s => s.PartOfSpeechTags)
            .Where(tag => WordClassMap.FromJmdictTag(tag) == primaryClass));

    /// <summary>One row of the kana-only "conjugations" table.</summary>
    public sealed record ConjugationRow(string Label, string Plain, string Polite);

    /// <summary>A conjugated form as displayed: its spelling (kanji where the
    /// headword has them) and its kana reading.</summary>
    public sealed record ConjugationCell(string Display, string Reading)
    {
        public bool HasReading => Display != Reading;
    }

    /// <summary>One row of the display table; a null cell means the form
    /// doesn't exist for the word.</summary>
    public sealed record ConjugationDisplayRow(string Label, ConjugationCell? Plain, ConjugationCell? Polite);

    private static readonly (string Label, string PlainForm, string? PoliteForm)[] TableSpec =
    [
        ("Non-past", "__dictionary__", "polite"),
        ("Past", "past", "polite past"),
        ("Negative", "negative", "polite negative"),
        ("Past neg.", "negative past", "polite negative past"),
        ("Te-form", "te-form", "polite te-form"),
        ("Negative te-form", "negative te-form", null),
        ("Potential", "potential", "polite potential"),
        ("Passive", "passive", null),
        ("Causative", "causative", null),
        ("Causative passive", "causative passive", null),
        ("Volitional", "volitional", "polite volitional"),
        ("Want to", "want to", "polite want to"),
        ("Conditional (ba)", "conditional (ba)", null),
        ("Conditional (tara)", "conditional (tara)", "polite conditional (tara)"),
        ("Imperative", "imperative", null),
    ];

    /// <summary>Builds a Form/Plain/Polite table in kana — the same generated
    /// forms as <see cref="GenerateForms(string, WordClass)"/>, organized by row
    /// instead of keyed by surface text. Rows the word class doesn't have
    /// (e.g. no imperative for i-adjectives) are omitted.</summary>
    public static IReadOnlyList<ConjugationRow> GetConjugationTable(string dictionaryReading, WordClass wordClass) =>
        BuildKanaRows(dictionaryReading, wordClass, ConjugationVariant.None)
            .Select(r => new ConjugationRow(r.Label, r.Plain ?? "—", r.Polite ?? "—"))
            .ToList();

    /// <summary>The display table: forms spelled with the headword's kanji
    /// (食べます) next to their kana reading (たべます). Where a form's reading
    /// doesn't start with the headword's changing-free reading prefix (いい →
    /// よくない) the cell stays in kana rather than guess a spelling.</summary>
    public static IReadOnlyList<ConjugationDisplayRow> GetConjugationTable(
        string headword, string dictionaryReading, WordClass wordClass, ConjugationVariant variant = ConjugationVariant.None)
    {
        var spelling = Spelling.Create(headword, dictionaryReading, wordClass);
        ConjugationCell? Cell(string? kana) => kana == null ? null : new ConjugationCell(spelling.Apply(kana), kana);

        return BuildKanaRows(dictionaryReading, wordClass, variant)
            .Select(r => new ConjugationDisplayRow(r.Label, Cell(r.Plain), Cell(r.Polite)))
            .ToList();
    }

    private static List<(string Label, string? Plain, string? Polite)> BuildKanaRows(
        string dictionaryReading, WordClass wordClass, ConjugationVariant variant)
    {
        var forms = GenerateForms(dictionaryReading, wordClass, variant);
        if (forms.Count == 0) return [];

        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (text, name) in forms)
            byName.TryAdd(name, text);

        // Ichidan and 来る don't distinguish potential from passive (-られる) —
        // GenerateForms only emits the combined "potential/passive" name for
        // them; alias it onto both table rows.
        if (byName.TryGetValue("potential/passive", out var potentialPassive))
        {
            byName.TryAdd("potential", potentialPassive);
            byName.TryAdd("passive", potentialPassive);
        }

        var rows = new List<(string, string?, string?)>();
        foreach (var (label, plainKey, politeKey) in TableSpec)
        {
            var plain = plainKey == "__dictionary__" ? DictionaryFormForTable(dictionaryReading, wordClass, byName) : byName.GetValueOrDefault(plainKey);
            var polite = politeKey == null ? null : byName.GetValueOrDefault(politeKey);
            if (plain == null && polite == null) continue;
            rows.Add((label, plain, polite));
        }

        return rows;
    }

    // A する-noun's reading is just the noun (べんきょう), and a な-adjective's plain
    // predicate needs だ, so neither reading alone is the non-past form.
    private static string DictionaryFormForTable(string reading, WordClass wordClass, Dictionary<string, string> byName) => wordClass switch
    {
        WordClass.SuruVerb when !reading.EndsWith("する", StringComparison.Ordinal) => reading + "する",
        WordClass.NaAdjective => byName.GetValueOrDefault("plain", reading),
        _ => reading,
    };

    /// <summary>Maps a kana form onto the headword's spelling: the headword's
    /// kanji part replaces the matching start of the reading (食/た + べます).</summary>
    private readonly record struct Spelling(string KanjiPrefix, string ReadingPrefix, bool IsKuru)
    {
        public static Spelling Create(string headword, string reading, WordClass wordClass)
        {
            var none = new Spelling("", "", false);
            if (headword.Length == 0 || headword == reading) return none;

            // 来 stands for く/こ/き, so only what precedes 来る is fixed (持って来る).
            if (wordClass == WordClass.KuruVerb
                && headword.EndsWith("来る", StringComparison.Ordinal)
                && reading.EndsWith("くる", StringComparison.Ordinal))
                return new Spelling(headword[..^2], reading[..^2], true);

            // The okurigana is the longest common suffix; what precedes it is
            // the kanji part and the reading it stands for.
            var suffix = 0;
            while (suffix < headword.Length - 1 && suffix < reading.Length - 1
                   && headword[headword.Length - 1 - suffix] == reading[reading.Length - 1 - suffix])
                suffix++;

            return new Spelling(headword[..^suffix], reading[..^suffix], false);
        }

        public string Apply(string kana)
        {
            if (ReadingPrefix.Length == 0 && !IsKuru) return kana;
            if (!kana.StartsWith(ReadingPrefix, StringComparison.Ordinal)) return kana;

            var rest = kana[ReadingPrefix.Length..];
            if (!IsKuru) return KanjiPrefix + rest;

            return rest.Length > 0 && rest[0] is 'く' or 'こ' or 'き'
                ? KanjiPrefix + "来" + rest[1..]
                : kana;
        }
    }
}
