using System.Text.RegularExpressions;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// The Spanish JMdict translators write a loanword's source word into the meaning
/// itself: タイムリー "(eng: timely) oportuno", アーチェリー "tiro con arco (eng:
/// archery)". About 7% of Spanish senses carry one, almost all of them katakana
/// words; French has a handful, German none. Compact views (the Look Up popup,
/// search previews) drop the note to make room for meanings; the word page keeps
/// the gloss as JMdict has it. "(lit: …)" is a literal translation, not an origin,
/// and stays.
/// </summary>
public static partial class GlossNotes
{
    /// <summary><paramref name="gloss"/> without its loanword-origin notes. A gloss
    /// that is nothing but a note comes back unchanged rather than empty.</summary>
    public static string WithoutOrigin(string gloss)
    {
        if (!gloss.Contains(':')) return gloss;
        var stripped = Spaces().Replace(OriginNote().Replace(gloss, " "), " ").Trim();
        return stripped.Length > 0 ? stripped : gloss;
    }

    // ISO 639-2 codes as the translators write them (plus "ale", their alemán).
    // An explicit list so other parenthesised notes never match.
    [GeneratedRegex(@"\s*\((?:eng|fre|fra|ger|deu|ale|ita|lat|dut|nld|por|rus|gre|ell|spa|chi|zho|kor|ara|san|hin|swe|nor|dan|pol|tur|per|heb|may|tha|vie|hun|fin|cze|ces|rum|ron):[^()]*\)\s*")]
    private static partial Regex OriginNote();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
