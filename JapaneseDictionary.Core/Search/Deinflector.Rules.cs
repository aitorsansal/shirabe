namespace JapaneseDictionary.Core.Search;

/// <summary>What kind of word a (partially) deinflected text currently is.
/// The first six are dictionary forms a search can land on; the rest are
/// intermediate steps that only further rules may consume.</summary>
[Flags]
internal enum InflectionType
{
    None = 0,
    Ichidan = 1 << 0,
    Godan = 1 << 1,
    Kuru = 1 << 2,
    Suru = 1 << 3,
    IAdjective = 1 << 4,
    NaAdjective = 1 << 5,

    /// <summary>A ます-form (食べます), reduced to its stem by the ます rule.</summary>
    Masu = 1 << 6,

    /// <summary>The 連用形 stem (食べ, 書き). Never a starting point: a bare query
    /// like はし must not turn into 走る, so only ます/たい rules produce it.</summary>
    Stem = 1 << 7,

    Te = 1 << 8,
    Past = 1 << 9,

    DictionaryForm = Ichidan | Godan | Kuru | Suru | IAdjective | NaAdjective,
    Any = DictionaryForm | Masu | Te | Past,
}

/// <param name="Reasons">Rules applied, outermost first (食べなかった: past,
/// negative). Empty for the untouched query.</param>
internal sealed record Deinflection(string Text, InflectionType Type, IReadOnlyList<string> Reasons);

public static partial class Deinflector
{
    private const int MaxSteps = 8;

    /// <summary>
    /// Undoes conjugation endings on <paramref name="text"/>, repeatedly, and
    /// returns every candidate dictionary form with the word kinds it may be
    /// (e.g. 食べさせられなかった -&gt; 食べる as an ichidan verb via past,
    /// negative, passive, causative). Rules only touch the kana ending, so
    /// kanji and kana input work alike. Candidates aren't checked against the
    /// dictionary here; most don't exist and are dropped by the caller's lookup.
    /// </summary>
    internal static IReadOnlyList<Deinflection> Deinflect(string text)
    {
        var results = new List<Deinflection>();
        var seen = new HashSet<(string, InflectionType)>();
        var queue = new Queue<Deinflection>();
        queue.Enqueue(new Deinflection(text, InflectionType.Any, []));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Reasons.Count >= MaxSteps) continue;

            foreach (var rule in Rules)
            {
                if ((current.Type & rule.In) == 0 || !current.Text.EndsWith(rule.From, StringComparison.Ordinal))
                    continue;

                var deinflected = current.Text[..^rule.From.Length] + rule.To;
                if (deinflected.Length == 0 || !seen.Add((deinflected, rule.Out)))
                    continue;

                var reasons = rule.Reason is null ? current.Reasons : [.. current.Reasons, rule.Reason];
                var next = new Deinflection(deinflected, rule.Out, reasons);
                results.Add(next);
                queue.Enqueue(next);
            }
        }

        return results;
    }

    /// <param name="Reason">Shown to the user; null for bookkeeping steps such as
    /// a stem going back to its dictionary form.</param>
    private readonly record struct Rule(string From, string To, InflectionType In, InflectionType Out, string? Reason);

    // Declared before Rules: static fields initialize in declaration order, and
    // BuildRules reads this table.
    private readonly record struct GodanRuleRow(string U, string A, string I, string E, string O, string Te, string Ta);

    private static readonly GodanRuleRow[] GodanRuleRows =
    [
        new("う", "わ", "い", "え", "お", "って", "った"),
        new("く", "か", "き", "け", "こ", "いて", "いた"),
        new("ぐ", "が", "ぎ", "げ", "ご", "いで", "いだ"),
        new("す", "さ", "し", "せ", "そ", "して", "した"),
        new("つ", "た", "ち", "て", "と", "って", "った"),
        new("ぬ", "な", "に", "ね", "の", "んで", "んだ"),
        new("ぶ", "ば", "び", "べ", "ぼ", "んで", "んだ"),
        new("む", "ま", "み", "め", "も", "んで", "んだ"),
        new("る", "ら", "り", "れ", "ろ", "って", "った"),
    ];

    private static readonly Rule[] Rules = BuildRules();

    private static Rule[] BuildRules()
    {
        const InflectionType any = InflectionType.Any;
        const InflectionType past = InflectionType.Past;
        const InflectionType te = InflectionType.Te;
        const InflectionType masu = InflectionType.Masu;
        const InflectionType stem = InflectionType.Stem;
        const InflectionType v1 = InflectionType.Ichidan;
        const InflectionType v5 = InflectionType.Godan;
        const InflectionType vk = InflectionType.Kuru;
        const InflectionType vs = InflectionType.Suru;
        const InflectionType adjI = InflectionType.IAdjective;
        const InflectionType adjNa = InflectionType.NaAdjective;

        var rules = new List<Rule>
        {
            // Endings that wrap a whole verb form, reached before the verb itself.
            new("たら", "た", any, past, "conditional (tara)"),
            new("だら", "だ", any, past, "conditional (tara)"),
            new("たり", "た", any, past, "representative (tari)"),
            new("だり", "だ", any, past, "representative (tari)"),

            // Polite ます and its own conjugations.
            new("ます", "", masu, stem, "polite"),
            new("ません", "ます", any, masu, "negative"),
            new("ました", "ます", past, masu, "past"),
            new("ませんでした", "ます", past, masu, "negative past"),
            new("ましょう", "ます", any, masu, "volitional"),
            new("まして", "ます", te, masu, "te-form"),

            new("たい", "", adjI, stem, "want to"),
            new("ないで", "ない", te, adjI, "te-form"),

            // て-form auxiliaries, which conjugate as verbs themselves.
            new("ている", "て", v1, te, "progressive"),
            new("でいる", "で", v1, te, "progressive"),
            new("てる", "て", v1, te, "progressive"),
            new("でる", "で", v1, te, "progressive"),
            new("てしまう", "て", v5, te, "completed"),
            new("でしまう", "で", v5, te, "completed"),
            new("ちゃう", "て", v5, te, "completed"),
            new("じゃう", "で", v5, te, "completed"),
            new("ておく", "て", v5, te, "in advance"),
            new("でおく", "で", v5, te, "in advance"),
            new("とく", "て", v5, te, "in advance"),
            new("どく", "で", v5, te, "in advance"),

            // Ichidan: 食べる.
            new("", "る", stem, v1, null),
            new("ない", "る", adjI, v1, "negative"),
            new("られる", "る", v1, v1, "passive/potential"),
            new("れる", "る", v1, v1, "potential"),
            new("させる", "る", v1, v1, "causative"),
            new("れば", "る", any, v1, "conditional (ba)"),
            new("よう", "る", any, v1, "volitional"),
            new("ろ", "る", any, v1, "imperative"),
            new("よ", "る", any, v1, "imperative"),
            new("て", "る", te, v1, "te-form"),
            new("た", "る", past, v1, "past"),

            // する and noun + する.
            new("し", "する", stem, vs, null),
            new("しない", "する", adjI, vs, "negative"),
            new("される", "する", v1, vs, "passive"),
            new("させる", "する", v1, vs, "causative"),
            new("できる", "する", v1, vs, "potential"),
            new("すれば", "する", any, vs, "conditional (ba)"),
            new("しよう", "する", any, vs, "volitional"),
            new("しろ", "する", any, vs, "imperative"),
            new("せよ", "する", any, vs, "imperative"),
            new("して", "する", te, vs, "te-form"),
            new("した", "する", past, vs, "past"),

            // い-adjectives (and the ない that verb negatives end in).
            new("くない", "い", adjI, adjI, "negative"),
            new("かった", "い", past, adjI, "past"),
            new("くて", "い", te, adjI, "te-form"),
            new("く", "い", any, adjI, "adverbial"),
            new("ければ", "い", any, adjI, "conditional (ba)"),
            new("いです", "い", any, adjI, "polite"),
            new("かったです", "かった", any, past, "polite"),

            // な-adjectives: the dictionary form is the bare stem (静か).
            new("だ", "", any, adjNa, null),
            new("です", "", any, adjNa, "polite"),
            new("な", "", any, adjNa, "attributive"),
            new("に", "", any, adjNa, "adverbial"),
            new("で", "", te, adjNa, "te-form"),
            new("なら", "", any, adjNa, "conditional"),
            new("だった", "", past, adjNa, "past"),
            new("でした", "", past, adjNa, "polite past"),
            new("じゃない", "", adjI, adjNa, "negative"),
            new("ではない", "", adjI, adjNa, "negative"),
            new("じゃなかった", "", past, adjNa, "negative past"),
            new("ではなかった", "", past, adjNa, "negative past"),
            new("じゃありません", "", any, adjNa, "polite negative"),
            new("ではありません", "", any, adjNa, "polite negative"),

            // 行く: regular -ku verb except for its て/た forms (行って, not 行いて).
            new("行って", "行く", te, v5, "te-form"),
            new("行った", "行く", past, v5, "past"),
            new("いって", "いく", te, v5, "te-form"),
            new("いった", "いく", past, v5, "past"),

            // -aru honorific verbs: いらっしゃいます, not いらっしゃります.
            new("い", "る", stem, v5, null),
            // ...and their imperative (ください, なさい, いらっしゃい). Kept to these
            // endings: a bare い -> る rule would read はい as 張る.
            new("さい", "さる", any, v5, "imperative"),
            new("しゃい", "しゃる", any, v5, "imperative"),
        };

        // 来る changes its stem vowel, so every form is spelled out, once for the
        // kanji and once for the kana spelling (also covers やって来る and co.).
        foreach (var (dictionary, ko, ki, ku) in new[] { ("来る", "来", "来", "来"), ("くる", "こ", "き", "く") })
        {
            rules.Add(new(ki, dictionary, stem, vk, null));
            rules.Add(new(ko + "ない", dictionary, adjI, vk, "negative"));
            rules.Add(new(ko + "られる", dictionary, v1, vk, "passive/potential"));
            rules.Add(new(ko + "れる", dictionary, v1, vk, "potential"));
            rules.Add(new(ko + "させる", dictionary, v1, vk, "causative"));
            rules.Add(new(ku + "れば", dictionary, any, vk, "conditional (ba)"));
            rules.Add(new(ko + "よう", dictionary, any, vk, "volitional"));
            rules.Add(new(ko + "い", dictionary, any, vk, "imperative"));
            rules.Add(new(ki + "て", dictionary, te, vk, "te-form"));
            rules.Add(new(ki + "た", dictionary, past, vk, "past"));
        }

        foreach (var row in GodanRuleRows)
        {
            rules.Add(new(row.I, row.U, stem, v5, null));
            rules.Add(new(row.A + "ない", row.U, adjI, v5, "negative"));
            rules.Add(new(row.A + "れる", row.U, v1, v5, "passive"));
            rules.Add(new(row.A + "せる", row.U, v1, v5, "causative"));
            rules.Add(new(row.E + "る", row.U, v1, v5, "potential"));
            rules.Add(new(row.E + "ば", row.U, any, v5, "conditional (ba)"));
            rules.Add(new(row.E, row.U, any, v5, "imperative"));
            rules.Add(new(row.O + "う", row.U, any, v5, "volitional"));
            rules.Add(new(row.Te, row.U, te, v5, "te-form"));
            rules.Add(new(row.Ta, row.U, past, v5, "past"));
        }

        return [.. rules];
    }
}
