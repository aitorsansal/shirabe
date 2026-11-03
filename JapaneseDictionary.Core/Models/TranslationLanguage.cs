using System.Globalization;

namespace JapaneseDictionary.Core.Models;

/// <summary>
/// A non-English language the dictionary carries glosses and sentence
/// translations for. <see cref="TranslationLanguages.All"/> is the one place
/// to add another: the DataBuilder, the repository and the settings page all
/// read it.
/// </summary>
/// <param name="Code">ISO 639-2/B code as JMdict writes it ("spa", "fre", "ger"); also what the database stores.</param>
/// <param name="TatoebaCode">Tatoeba's code for the same language ("spa", "fra", "deu").</param>
/// <param name="Endonym">The language's own name, shown as-is in every UI language.</param>
/// <param name="Badge">Short upper-case label for sentence lines ("ES").</param>
/// <param name="Iso1">Two-letter ISO 639-1 code, matched against the device culture.</param>
public sealed record TranslationLanguage(string Code, string TatoebaCode, string Endonym, string Badge, string Iso1);

public static class TranslationLanguages
{
    public static readonly IReadOnlyList<TranslationLanguage> All =
    [
        new("spa", "spa", "Español", "ES", "es"),
        new("fre", "fra", "Français", "FR", "fr"),
        new("ger", "deu", "Deutsch", "DE", "de"),
    ];

    public static TranslationLanguage? Find(string code) =>
        All.FirstOrDefault(l => l.Code == code);

    /// <summary>The languages pre-selected on first run for a device culture
    /// name such as "es-ES", "ca" or "en-GB": its own language when we carry
    /// it, Spanish for Catalan, none otherwise.</summary>
    public static IReadOnlyList<string> DefaultFor(string? cultureName)
    {
        if (string.IsNullOrEmpty(cultureName)) return [];
        var iso1 = cultureName.Split('-', '_')[0].ToLowerInvariant();
        if (iso1 == "ca") iso1 = "es";
        var match = All.FirstOrDefault(l => l.Iso1 == iso1);
        return match == null ? [] : [match.Code];
    }

    /// <summary>Keeps only known codes, in <see cref="All"/> order.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> codes)
    {
        var set = codes.ToHashSet(StringComparer.Ordinal);
        return All.Where(l => set.Contains(l.Code)).Select(l => l.Code).ToList();
    }
}
