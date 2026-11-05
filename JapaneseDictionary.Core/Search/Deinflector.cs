using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Generates every conjugated surface form of a word from its dictionary
/// reading and <see cref="WordClass"/>. Used two ways: (1) search matches a
/// typed query against a candidate's generated forms to recognize conjugated
/// input (e.g. たべた -&gt; 食べる's "past" form), and (2) the same table drives
/// the "conjugations" display for a word (see Deinflector.ConjugationTable.cs),
/// since both need the same dictionary-form -&gt; conjugated-form mapping.
/// </summary>
public static partial class Deinflector
{
    /// <summary>All conjugated forms of <paramref name="dictionaryReading"/>, keyed
    /// by the surface text, mapping to a short form name (e.g. "past", "negative").
    /// Empty for word classes that don't conjugate.</summary>
    public static IReadOnlyDictionary<string, string> GenerateForms(string dictionaryReading, WordClass wordClass) =>
        GenerateForms(dictionaryReading, wordClass, ConjugationVariant.None);

    /// <summary>As <see cref="GenerateForms(string, WordClass)"/>, with the
    /// exceptions of an irregular verb subclass applied. Only the conjugation
    /// table uses a variant; search keeps the regular forms.</summary>
    public static IReadOnlyDictionary<string, string> GenerateForms(string dictionaryReading, WordClass wordClass, ConjugationVariant variant)
    {
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (wordClass)
        {
            case WordClass.IchidanVerb:
                AddIchidanForms(forms, dictionaryReading, variant);
                break;
            case WordClass.IAdjective:
                AddIAdjectiveForms(forms, dictionaryReading);
                break;
            case WordClass.SuruVerb:
                AddSuruForms(forms, dictionaryReading, variant);
                break;
            case WordClass.KuruVerb:
                AddKuruForms(forms, dictionaryReading);
                break;
            case WordClass.NaAdjective:
                AddNaAdjectiveForms(forms, dictionaryReading);
                break;
            default:
                if (GodanRows.TryGetValue(wordClass, out var row))
                    AddGodanForms(forms, dictionaryReading, row, wordClass, variant);
                break;
        }

        return forms;
    }

    /// <summary>Returns the form name if <paramref name="query"/> is a valid
    /// conjugated form of <paramref name="dictionaryReading"/>, else null.</summary>
    public static string? TryGetFormName(string query, string dictionaryReading, WordClass wordClass) =>
        GenerateForms(dictionaryReading, wordClass).GetValueOrDefault(query);

    /// <summary>Sets the surface text for a form name, dropping whatever text
    /// the regular rule produced for it.</summary>
    private static void Replace(Dictionary<string, string> forms, string name, string? text)
    {
        foreach (var key in forms.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList())
            forms.Remove(key);
        if (text != null) forms[text] = name;
    }

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

    private static void AddGodanForms(Dictionary<string, string> forms, string reading, GodanRow row, WordClass wordClass, ConjugationVariant variant)
    {
        var root = reading[..^1];

        // くださる, いらっしゃる, なさる...: the polite stem is い (くださいます).
        var polite = variant == ConjugationVariant.GodanAru && wordClass == WordClass.GodanVerbRu ? "い" : row.I;

        forms[root + row.A + "ない"] = "negative";
        forms[root + row.A + "なかった"] = "negative past";
        forms[root + row.A + "ないで"] = "negative te-form";
        forms[root + polite + "ます"] = "polite";
        forms[root + polite + "ません"] = "polite negative";
        forms[root + polite + "ました"] = "polite past";
        forms[root + polite + "ませんでした"] = "polite negative past";
        forms[root + polite + "まして"] = "polite te-form";
        forms[root + polite + "ましょう"] = "polite volitional";
        forms[root + polite + "ましたら"] = "polite conditional (tara)";
        forms[root + row.I + "たい"] = "want to";
        forms[root + row.I + "たいです"] = "polite want to";
        forms[root + row.E + "ば"] = "conditional (ba)";
        forms[root + row.E + "る"] = "potential";
        forms[root + row.E + "ます"] = "polite potential";
        forms[root + row.O + "う"] = "volitional";
        forms[root + row.A + "れる"] = "passive";
        forms[root + row.A + "せる"] = "causative";
        forms[root + row.A + "せられる"] = "causative passive";
        forms[root + row.A + "れます"] = "polite passive";
        forms[root + row.A + "せます"] = "polite causative";
        forms[root + row.A + "せられます"] = "polite causative passive";
        forms[root + row.Te] = "te-form";
        forms[root + row.Ta] = "past";
        forms[root + row.Ta + "ら"] = "conditional (tara)";
        forms[root + row.Ta + "り"] = "representative (tari)";
        forms[root + row.E] = "imperative";

        switch (variant)
        {
            case ConjugationVariant.GodanAruIrregular when wordClass == WordClass.GodanVerbRu:
                // ある: the negative is just ない, and there is no potential.
                var stem = reading.EndsWith("ある", StringComparison.Ordinal) ? reading[..^2] : root;
                Replace(forms, "negative", stem + "ない");
                Replace(forms, "negative past", stem + "なかった");
                Replace(forms, "negative te-form", stem + "ないで");
                Replace(forms, "potential", null);
                Replace(forms, "polite potential", null);
                break;
            case ConjugationVariant.GodanAru when wordClass == WordClass.GodanVerbRu:
                Replace(forms, "imperative", root + "い");
                Replace(forms, "polite potential", null);
                break;
            case ConjugationVariant.GodanUArchaic when wordClass == WordClass.GodanVerbU:
                // 問う, 請う, 乞う: te/ta keep the う (問うて, not 問って).
                Replace(forms, "te-form", reading + "て");
                Replace(forms, "past", reading + "た");
                Replace(forms, "conditional (tara)", reading + "たら");
                Replace(forms, "representative (tari)", reading + "たり");
                break;
        }
    }

    private static void AddIchidanForms(Dictionary<string, string> forms, string reading, ConjugationVariant variant)
    {
        // 信ずる (vz) conjugates like the ichidan 信じる everywhere but the plain
        // non-past, which the table takes from the dictionary form itself.
        if (variant == ConjugationVariant.Zuru && reading.EndsWith("ずる", StringComparison.Ordinal))
            reading = reading[..^2] + "じる";

        var root = reading[..^1]; // drop the trailing る

        forms[root + "ない"] = "negative";
        forms[root + "なかった"] = "negative past";
        forms[root + "ないで"] = "negative te-form";
        forms[root + "ます"] = "polite";
        forms[root + "ません"] = "polite negative";
        forms[root + "ました"] = "polite past";
        forms[root + "ませんでした"] = "polite negative past";
        forms[root + "まして"] = "polite te-form";
        forms[root + "ましょう"] = "polite volitional";
        forms[root + "ましたら"] = "polite conditional (tara)";
        forms[root + "たい"] = "want to";
        forms[root + "たいです"] = "polite want to";
        forms[root + "れば"] = "conditional (ba)";
        forms[root + "よう"] = "volitional";
        forms[root + "られる"] = "potential/passive";
        forms[root + "られます"] = "polite potential";
        forms[root + "させる"] = "causative";
        forms[root + "させられる"] = "causative passive";
        // The polite passive (root + られます) is the "polite potential" entry above.
        forms[root + "させます"] = "polite causative";
        forms[root + "させられます"] = "polite causative passive";
        forms[root + "て"] = "te-form";
        forms[root + "た"] = "past";
        forms[root + "たら"] = "conditional (tara)";
        forms[root + "たり"] = "representative (tari)";
        forms[root + "ろ"] = "imperative";

        if (variant == ConjugationVariant.IchidanKureru)
            Replace(forms, "imperative", root); // くれ, not くれろ
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
        forms[root + "くなくて"] = "negative te-form";
        forms[root + "く"] = "adverbial";
        forms[root + "ければ"] = "conditional (ba)";
        forms[root + "かったら"] = "conditional (tara)";
        forms[root + "くなければ"] = "negative conditional";
    }

    private static void AddSuruForms(Dictionary<string, string> forms, string reading, ConjugationVariant variant)
    {
        // JMdict tags nouns that take する (勉強, べんきょう) with the same class as
        // する verbs themselves (愛する), so the reading may or may not include it.
        var root = reading.EndsWith("する", StringComparison.Ordinal) ? reading[..^2] : reading;

        forms[root + "しない"] = "negative";
        forms[root + "しなかった"] = "negative past";
        forms[root + "しないで"] = "negative te-form";
        forms[root + "します"] = "polite";
        forms[root + "しません"] = "polite negative";
        forms[root + "しました"] = "polite past";
        forms[root + "しませんでした"] = "polite negative past";
        forms[root + "しまして"] = "polite te-form";
        forms[root + "しましょう"] = "polite volitional";
        forms[root + "しましたら"] = "polite conditional (tara)";
        forms[root + "したい"] = "want to";
        forms[root + "したいです"] = "polite want to";
        forms[root + "すれば"] = "conditional (ba)";
        forms[root + "しよう"] = "volitional";
        forms[root + "できる"] = "potential";
        forms[root + "できます"] = "polite potential";
        forms[root + "される"] = "passive";
        forms[root + "させる"] = "causative";
        forms[root + "させられる"] = "causative passive";
        forms[root + "されます"] = "polite passive";
        forms[root + "させます"] = "polite causative";
        forms[root + "させられます"] = "polite causative passive";
        forms[root + "して"] = "te-form";
        forms[root + "した"] = "past";
        forms[root + "したら"] = "conditional (tara)";
        forms[root + "しろ"] = "imperative";

        if (variant == ConjugationVariant.SuruS)
        {
            // 愛する, 察する: the old sa-row verb (愛さない, 愛せる, 愛そう, 愛せ).
            Replace(forms, "negative", root + "さない");
            Replace(forms, "negative past", root + "さなかった");
            Replace(forms, "negative te-form", root + "さないで");
            Replace(forms, "potential", root + "せる");
            Replace(forms, "polite potential", root + "せます");
            Replace(forms, "volitional", root + "そう");
            Replace(forms, "imperative", root + "せ");
        }
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
        ("こないで", "negative te-form"),
        ("きます", "polite"),
        ("きません", "polite negative"),
        ("きました", "polite past"),
        ("きませんでした", "polite negative past"),
        ("きまして", "polite te-form"),
        ("きましょう", "polite volitional"),
        ("きましたら", "polite conditional (tara)"),
        ("きたい", "want to"),
        ("きたいです", "polite want to"),
        ("くれば", "conditional (ba)"),
        ("こよう", "volitional"),
        ("こられる", "potential/passive"),
        ("こられます", "polite potential"),
        ("こさせる", "causative"),
        ("こさせられる", "causative passive"),
        ("こさせます", "polite causative"),
        ("こさせられます", "polite causative passive"),
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
        forms[stem + "だったら"] = "conditional (tara)";
    }
}
