using System.Globalization;

using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;
using JapaneseDictionary.Resources.Localization;

namespace JapaneseDictionary.Services;

/// <summary>
/// UI strings that are built in code rather than read straight from
/// <see cref="AppResources"/>: counts, formatted messages, and labels that
/// arrive from Core in English (conjugation form names, table rows) or from
/// the database (the Favourites list's stored name).
/// </summary>
public static class UiText
{
    public static string Format(string format, params object[] args) =>
        string.Format(LanguageService.Culture, format, args);

    public static string WordCount(int count) =>
        count == 1 ? AppResources.Common_WordCountOne : Format(AppResources.Common_WordCountMany, count);

    public static string EntryCount(int count) =>
        count == 1 ? AppResources.Search_EntryCountOne : Format(AppResources.Search_EntryCountMany, count);

    /// <summary>System lists are stored with an English name; show it translated.</summary>
    public static string ListName(WordListSummary list) =>
        list.Id == UserDataRepository.FavoritesListId ? AppResources.Common_Favourites : list.Name;

    public static string WordClassName(WordClass wordClass) => wordClass switch
    {
        WordClass.IchidanVerb => AppResources.Class_Ichidan,
        WordClass.IAdjective => AppResources.Class_IAdjective,
        WordClass.NaAdjective => AppResources.Class_NaAdjective,
        WordClass.SuruVerb => AppResources.Class_Suru,
        WordClass.KuruVerb => AppResources.Class_Kuru,
        WordClass.GodanVerbU or WordClass.GodanVerbKu or WordClass.GodanVerbGu or WordClass.GodanVerbSu
            or WordClass.GodanVerbTsu or WordClass.GodanVerbNu or WordClass.GodanVerbBu or WordClass.GodanVerbMu
            or WordClass.GodanVerbRu or WordClass.GodanVerbIkuYuku => AppResources.Class_Godan,
        _ => AppResources.Class_Word,
    };

    /// <summary>A Deinflector form name ("past", "conditional (ba)") or a chain
    /// of them as SearchService joins them ("causative · passive · past"), in
    /// the UI language; unknown names pass through unchanged.</summary>
    public static string FormName(string? coreName) =>
        coreName == null ? "" : string.Join(" · ", coreName.Split(" · ").Select(part =>
            FormKeys.TryGetValue(part, out var key) ? Lookup(key, part) : part));

    /// <summary>A Deinflector conjugation table row label ("Non-past", "Te-form").</summary>
    public static string ConjugationLabel(string coreLabel) =>
        ConjugationKeys.TryGetValue(coreLabel, out var key) ? Lookup(key, coreLabel) : coreLabel;

    /// <summary>Plain-language name of a JMdict part-of-speech tag ("vs" → "noun
    /// that takes する"). Archaic verb classes share one description; an unknown
    /// tag is returned as is.</summary>
    public static string PartOfSpeechName(string tag) =>
        AppResources.ResourceManager.GetString($"Pos_{tag.Replace('-', '_')}", LanguageService.Culture)
        ?? (tag.Length > 1 && tag[0] == 'v' && tag[1] is '2' or '4'
            ? AppResources.Pos__archaicVerb
            : tag);

    private static string Lookup(string key, string fallback) =>
        AppResources.ResourceManager.GetString(key, LanguageService.Culture) ?? fallback;

    // Keyed by the exact English strings Core's Deinflector emits. A name added
    // there without an entry here just shows in English.
    private static readonly Dictionary<string, string> FormKeys = new(StringComparer.Ordinal)
    {
        ["negative"] = nameof(AppResources.Form_Negative),
        ["negative past"] = nameof(AppResources.Form_NegativePast),
        ["polite"] = nameof(AppResources.Form_Polite),
        ["polite negative"] = nameof(AppResources.Form_PoliteNegative),
        ["polite past"] = nameof(AppResources.Form_PolitePast),
        ["polite negative past"] = nameof(AppResources.Form_PoliteNegativePast),
        ["want to"] = nameof(AppResources.Form_WantTo),
        ["conditional (ba)"] = nameof(AppResources.Form_ConditionalBa),
        ["potential"] = nameof(AppResources.Form_Potential),
        ["volitional"] = nameof(AppResources.Form_Volitional),
        ["passive"] = nameof(AppResources.Form_Passive),
        ["causative"] = nameof(AppResources.Form_Causative),
        ["causative passive"] = nameof(AppResources.Form_CausativePassive),
        ["te-form"] = nameof(AppResources.Form_TeForm),
        ["past"] = nameof(AppResources.Form_Past),
        ["conditional (tara)"] = nameof(AppResources.Form_ConditionalTara),
        ["representative (tari)"] = nameof(AppResources.Form_RepresentativeTari),
        ["imperative"] = nameof(AppResources.Form_Imperative),
        ["potential/passive"] = nameof(AppResources.Form_PotentialPassive),
        ["adverbial"] = nameof(AppResources.Form_Adverbial),
        ["negative conditional"] = nameof(AppResources.Form_NegativeConditional),
        ["plain"] = nameof(AppResources.Form_Plain),
        ["attributive"] = nameof(AppResources.Form_Attributive),
        // Deinflector.Rules.cs reasons not produced by GenerateForms.
        ["completed"] = nameof(AppResources.Form_Completed),
        ["conditional"] = nameof(AppResources.Form_Conditional),
        ["in advance"] = nameof(AppResources.Form_InAdvance),
        ["passive/potential"] = nameof(AppResources.Form_PassivePotential),
        ["progressive"] = nameof(AppResources.Form_Progressive),
    };

    private static readonly Dictionary<string, string> ConjugationKeys = new(StringComparer.Ordinal)
    {
        ["Non-past"] = nameof(AppResources.Conj_NonPast),
        ["Past"] = nameof(AppResources.Conj_Past),
        ["Negative"] = nameof(AppResources.Conj_Negative),
        ["Past neg."] = nameof(AppResources.Conj_PastNegative),
        ["Te-form"] = nameof(AppResources.Conj_TeForm),
        ["Potential"] = nameof(AppResources.Conj_Potential),
        ["Passive"] = nameof(AppResources.Conj_Passive),
        ["Causative"] = nameof(AppResources.Conj_Causative),
        ["Volitional"] = nameof(AppResources.Conj_Volitional),
        ["Conditional"] = nameof(AppResources.Conj_Conditional),
        ["Imperative"] = nameof(AppResources.Conj_Imperative),
    };
}
