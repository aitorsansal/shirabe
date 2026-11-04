using System.Globalization;
using System.Text;

namespace JapaneseDictionary.Core.Search;

/// <summary>Accent-insensitive text matching for Latin-script glosses, so "cafe"
/// finds "café" and "nino" finds "niño".</summary>
internal static class TextFolding
{
    /// <summary>Lower-cased text with combining marks removed ("Mädchen" → "madchen").</summary>
    public static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>SQL LIKE pattern (ESCAPE '\') that over-matches the folded query:
    /// SQLite's LIKE doesn't fold accents, but <c>_</c> matches any single
    /// character, so every letter that can carry an accent becomes <c>_</c>
    /// ("cafe" → <c>%_af_%</c>). Callers must re-check the real match on folded text.</summary>
    public static string AccentInsensitiveLikePattern(string query)
    {
        var folded = Fold(query.Trim());
        var sb = new StringBuilder(folded.Length + 2).Append('%');
        foreach (var c in folded)
        {
            sb.Append(c switch
            {
                '\\' => @"\\",
                '%' => @"\%",
                '_' => @"\_",
                'a' or 'e' or 'i' or 'o' or 'u' or 'n' or 'c' => "_",
                _ => c.ToString(),
            });
        }

        return sb.Append('%').ToString();
    }

    /// <summary>One character that isn't a digit or a Latin letter (Latin-1 through
    /// Latin Extended-B), as a GLOB class. The gloss separator U+001F counts as one.</summary>
    private const string WordBoundary = "[^0-9A-Za-zÀ-ɏ]";

    // The accented forms (both cases) that Fold turns into each base letter.
    private static readonly Dictionary<char, string> Variants = new()
    {
        ['a'] = "aàáâãäåāAÀÁÂÃÄÅĀ",
        ['e'] = "eèéêëēEÈÉÊËĒ",
        ['i'] = "iìíîïīIÌÍÎÏĪ",
        ['o'] = "oòóôõöōOÒÓÔÕÖŌ",
        ['u'] = "uùúûüūUÙÚÛÜŪ",
        ['n'] = "nñNÑ",
        ['c'] = "cçCÇ",
        ['y'] = "yýÿYÝŸ",
    };

    /// <summary>SQL GLOB pattern matching the folded query exactly, ignoring case and
    /// accents: each letter becomes a character class of its variants ("nino" →
    /// <c>*[nñNÑ][iìí…][nñNÑ][oòó…]*</c>). GLOB is slower per row than LIKE, so it runs
    /// after <see cref="AccentInsensitiveLikePattern"/>, which throws out most rows
    /// cheaply but lets far too many through on its own ("nino" is <c>%____%</c>).
    /// With <paramref name="wholeWord"/> the match must sit between non-letters, so
    /// the text it's tested against needs a space padded on both ends: a short query
    /// like "ab" is otherwise inside thousands of German glosses.</summary>
    public static string AccentInsensitiveGlobPattern(string query, bool wholeWord = false)
    {
        var folded = Fold(query.Trim());
        var sb = new StringBuilder(folded.Length * 8 + 2).Append('*');
        if (wholeWord) sb.Append(WordBoundary);
        foreach (var c in folded)
        {
            if (Variants.TryGetValue(c, out var variants))
                sb.Append('[').Append(variants).Append(']');
            else if (char.IsLetter(c) && char.ToUpperInvariant(c) != c)
                sb.Append('[').Append(c).Append(char.ToUpperInvariant(c)).Append(']');
            else if (c is '*' or '?' or '[')
                sb.Append('[').Append(c).Append(']');
            else
                sb.Append(c); // ']' and '^' are literal outside a class
        }

        if (wholeWord) sb.Append(WordBoundary);
        return sb.Append('*').ToString();
    }
}
