using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Generates every conjugated surface form of a word from its dictionary
/// reading and <see cref="WordClass"/>. Used two ways: (1) search matches a
/// typed query against a candidate's generated forms to recognize conjugated
/// input (e.g. たべた -&gt; 食べる's "past" form), and (2) the same table can
/// drive a "conjugations" display for a word later, since both need the same
/// dictionary-form -&gt; conjugated-form mapping.
/// </summary>
public static partial class Deinflector
{
    /// <summary>All conjugated forms of <paramref name="dictionaryReading"/>, keyed
    /// by the surface text, mapping to a short form name (e.g. "past", "negative").
    /// Empty for word classes that don't conjugate.</summary>
    public static IReadOnlyDictionary<string, string> GenerateForms(string dictionaryReading, WordClass wordClass)
    {
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (wordClass)
        {
            case WordClass.IchidanVerb:
                AddIchidanForms(forms, dictionaryReading);
                break;
            case WordClass.IAdjective:
                AddIAdjectiveForms(forms, dictionaryReading);
                break;
            case WordClass.SuruVerb:
                AddSuruForms(forms, dictionaryReading);
                break;
            case WordClass.KuruVerb:
                AddKuruForms(forms, dictionaryReading);
                break;
            case WordClass.NaAdjective:
                AddNaAdjectiveForms(forms, dictionaryReading);
                break;
            default:
                if (GodanRows.TryGetValue(wordClass, out var row))
                    AddGodanForms(forms, dictionaryReading, row);
                break;
        }

        return forms;
    }

    /// <summary>Returns the form name if <paramref name="query"/> is a valid
    /// conjugated form of <paramref name="dictionaryReading"/>, else null.</summary>
    public static string? TryGetFormName(string query, string dictionaryReading, WordClass wordClass) =>
        GenerateForms(dictionaryReading, wordClass).GetValueOrDefault(query);

    private readonly record struct GodanRow(string A, string I, string U, string E, string O, string Te, string Ta);

    private static readonly IReadOnlyDictionary<WordClass, GodanRow> GodanRows = new Dictionary<WordClass, GodanRow>
    {
        [WordClass.GodanVerbU] = new("わ", "い", "う", "え", "お", "って", "った"),
        [WordClass.GodanVerbKu] = new("か", "き", "く", "け", "こ", "いて", "いた"),
        [WordClass.GodanVerbGu] = new("が", "ぎ", "ぐ", "げ", "ご", "いで", "いだ"),
        [WordClass.GodanVerbSu] = new("さ", "し", "す", "せ", "そ", "して", "した"),
        [WordClass.GodanVerbTsu] = new("た", "ち", "つ", "て", "と", "って", "った"),
        [WordClass.GodanVerbNu] = new("な", "に", "ぬ", "ね", "の", "んで", "んだ"),
        [WordClass.GodanVerbBu] = new("ば", "び", "ぶ", "べ", "ぼ", "んで", "んだ"),
        [WordClass.GodanVerbMu] = new("ま", "み", "む", "め", "も", "んで", "んだ"),
        [WordClass.GodanVerbRu] = new("ら", "り", "る", "れ", "ろ", "って", "った"),
        // 行く/往く: a normal -ku verb for every stem except its te/ta form, which
        // is irregular (行って/行った, not the expected 行いて/行いた).
        [WordClass.GodanVerbIkuYuku] = new("か", "き", "く", "け", "こ", "って", "った"),
    };

    private static void AddGodanForms(Dictionary<string, string> forms, string reading, GodanRow row)
    {
        var root = reading[..^1];

        forms[root + row.A + "ない"] = "negative";
        forms[root + row.A + "なかった"] = "negative past";
        forms[root + row.I + "ます"] = "polite";
        forms[root + row.I + "ません"] = "polite negative";
        forms[root + row.I + "ました"] = "polite past";
        forms[root + row.I + "ませんでした"] = "polite negative past";
        forms[root + row.I + "たい"] = "want to";
        forms[root + row.E + "ば"] = "conditional (ba)";
        forms[root + row.E + "る"] = "potential";
        forms[root + row.O + "う"] = "volitional";
        forms[root + row.A + "れる"] = "passive";
        forms[root + row.A + "せる"] = "causative";
        forms[root + row.A + "せられる"] = "causative passive";
        forms[root + row.Te] = "te-form";
        forms[root + row.Ta] = "past";
        forms[root + row.Ta + "ら"] = "conditional (tara)";
        forms[root + row.Ta + "り"] = "representative (tari)";
        forms[root + row.E] = "imperative";
    }

    private static void AddIchidanForms(Dictionary<string, string> forms, string reading)
    {
        var root = reading[..^1]; // drop the trailing る

        forms[root + "ない"] = "negative";
        forms[root + "なかった"] = "negative past";
        forms[root + "ます"] = "polite";
        forms[root + "ません"] = "polite negative";
        forms[root + "ました"] = "polite past";
        forms[root + "ませんでした"] = "polite negative past";
        forms[root + "たい"] = "want to";
        forms[root + "れば"] = "conditional (ba)";
        forms[root + "よう"] = "volitional";
        forms[root + "られる"] = "potential/passive";
        forms[root + "させる"] = "causative";
        forms[root + "させられる"] = "causative passive";
        forms[root + "て"] = "te-form";
        forms[root + "た"] = "past";
        forms[root + "たら"] = "conditional (tara)";
        forms[root + "たり"] = "representative (tari)";
        forms[root + "ろ"] = "imperative";
    }

    private static void AddIAdjectiveForms(Dictionary<string, string> forms, string reading)
    {
        // いい (and compounds like かっこいい) conjugates from its older form よい.
        var root = reading.EndsWith("いい", StringComparison.Ordinal) ? reading[..^2] + "よ" : reading[..^1];

        forms[reading + "です"] = "polite";
        forms[root + "くない"] = "negative";
        forms[root + "くなかった"] = "negative past";
        forms[root + "かった"] = "past";
        forms[root + "かったです"] = "polite past";
        forms[root + "くないです"] = "polite negative";
        forms[root + "くなかったです"] = "polite negative past";
        forms[root + "くて"] = "te-form";
        forms[root + "く"] = "adverbial";
        forms[root + "ければ"] = "conditional (ba)";
        forms[root + "くなければ"] = "negative conditional";
    }

    private static void AddSuruForms(Dictionary<string, string> forms, string reading)
    {
        // JMdict tags nouns that take する (勉強, べんきょう) with the same class as
        // する verbs themselves (愛する), so the reading may or may not include it.
        var root = reading.EndsWith("する", StringComparison.Ordinal) ? reading[..^2] : reading;

        forms[root + "しない"] = "negative";
        forms[root + "しなかった"] = "negative past";
        forms[root + "します"] = "polite";
        forms[root + "しません"] = "polite negative";
        forms[root + "しました"] = "polite past";
        forms[root + "しませんでした"] = "polite negative past";
        forms[root + "したい"] = "want to";
        forms[root + "すれば"] = "conditional (ba)";
        forms[root + "しよう"] = "volitional";
        forms[root + "できる"] = "potential";
        forms[root + "される"] = "passive";
        forms[root + "させる"] = "causative";
        forms[root + "させられる"] = "causative passive";
        forms[root + "して"] = "te-form";
        forms[root + "した"] = "past";
        forms[root + "したら"] = "conditional (tara)";
        forms[root + "しろ"] = "imperative";
    }

    /// <summary>来る's reading itself changes across forms (くる/こない/きた/...), so
    /// this spells each form out rather than using a root+suffix pattern. The
    /// prefix keeps compounds like やってくる working.</summary>
    private static void AddKuruForms(Dictionary<string, string> forms, string reading)
    {
        if (!reading.EndsWith("くる", StringComparison.Ordinal)) return;
        var prefix = reading[..^2];

        foreach (var (form, name) in KuruForms)
            forms[prefix + form] = name;
    }

    private static readonly (string Form, string Name)[] KuruForms =
    [
        ("こない", "negative"),
        ("こなかった", "negative past"),
        ("きます", "polite"),
        ("きません", "polite negative"),
        ("きました", "polite past"),
        ("きませんでした", "polite negative past"),
        ("きたい", "want to"),
        ("くれば", "conditional (ba)"),
        ("こよう", "volitional"),
        ("こられる", "potential/passive"),
        ("こさせる", "causative"),
        ("きて", "te-form"),
        ("きた", "past"),
        ("きたら", "conditional (tara)"),
        ("こい", "imperative"),
    ];

    private static void AddNaAdjectiveForms(Dictionary<string, string> forms, string stem)
    {
        forms[stem + "だ"] = "plain";
        forms[stem + "です"] = "polite";
        forms[stem + "だった"] = "past";
        forms[stem + "でした"] = "polite past";
        forms[stem + "じゃない"] = "negative";
        forms[stem + "じゃありません"] = "polite negative";
        forms[stem + "じゃなかった"] = "negative past";
        forms[stem + "じゃありませんでした"] = "polite negative past";
        forms[stem + "で"] = "te-form";
        forms[stem + "な"] = "attributive";
        forms[stem + "なら"] = "conditional (ba)";
    }

    /// <summary>One row of a "conjugations" display table.</summary>
    public sealed record ConjugationRow(string Label, string Plain, string Polite);

    private static readonly (string Label, string PlainForm, string? PoliteForm)[] TableSpec =
    [
        ("Non-past", "__dictionary__", "polite"),
        ("Past", "past", "polite past"),
        ("Negative", "negative", "polite negative"),
        ("Past neg.", "negative past", "polite negative past"),
        ("Te-form", "te-form", null),
        ("Potential", "potential", null),
        ("Passive", "passive", null),
        ("Causative", "causative", null),
        ("Volitional", "volitional", null),
        ("Conditional", "conditional (ba)", null),
        ("Imperative", "imperative", null),
    ];

    /// <summary>Builds a Form/Plain/Polite table for display — the same
    /// generated forms as <see cref="GenerateForms"/>, organized by row
    /// instead of keyed by surface text. Rows the word class doesn't have
    /// (e.g. no imperative for i-adjectives) are omitted.</summary>
    public static IReadOnlyList<ConjugationRow> GetConjugationTable(string dictionaryReading, WordClass wordClass)
    {
        var forms = GenerateForms(dictionaryReading, wordClass);
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

        var rows = new List<ConjugationRow>();
        foreach (var (label, plainKey, politeKey) in TableSpec)
        {
            var plain = plainKey == "__dictionary__" ? DictionaryFormForTable(dictionaryReading, wordClass, byName) : byName.GetValueOrDefault(plainKey, "—");
            var polite = politeKey == null ? "—" : byName.GetValueOrDefault(politeKey, "—");
            if (plain == "—" && polite == "—") continue;
            rows.Add(new ConjugationRow(label, plain, polite));
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
}
