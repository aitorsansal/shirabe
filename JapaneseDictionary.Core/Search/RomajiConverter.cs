using System.Text;

namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Converts romaji (Latin-alphabet Japanese input, e.g. from a normal
/// keyboard) into hiragana or katakana, IME-style, so search doesn't require
/// a Japanese keyboard layout. Uses a standard Hepburn-style syllable table
/// with a greedy longest-match scan.
/// </summary>
public static class RomajiConverter
{
    public static string ToHiragana(string romaji) => Convert(romaji, Hiragana);
    public static string ToKatakana(string romaji) => Convert(romaji, Katakana);

    /// <summary>True if <paramref name="text"/> is just <paramref name="kana"/>
    /// spelled in romaji, ignoring case, spaces and hyphens. JMdict sometimes
    /// lists the romanized word itself as the first gloss (お食い初め:
    /// "okuizome", then "weaning ceremony"), which is useless as a meaning.</summary>
    public static bool IsRomanizationOf(string text, string kana)
    {
        if (text.Length == 0 || kana.Length == 0) return false;
        var compact = text.Replace(" ", "").Replace("-", "");
        if (compact.Any(c => !char.IsAsciiLetter(c))) return false;
        return ToHiragana(compact) == kana || ToKatakana(compact) == kana;
    }

    private static string Convert(string romaji, IReadOnlyDictionary<string, string> table)
    {
        var input = romaji.ToLowerInvariant();
        var output = new StringBuilder(input.Length);
        var i = 0;

        while (i < input.Length)
        {
            // Sokuon: a doubled consonant (other than n) becomes a small tsu
            // before the syllable that follows, e.g. "kitte" -> きって.
            if (i + 1 < input.Length && input[i] == input[i + 1] && IsConsonant(input[i]) && input[i] != 'n')
            {
                output.Append(table["xtu"]);
                i++;
                continue;
            }

            // "n" not followed by a vowel or 'y' is a standalone syllabic ん.
            // "n'" explicitly terminates it (e.g. "kon'ya" -> こんや, not こにゃ).
            if (input[i] == 'n' && (i + 1 == input.Length || input[i + 1] == '\'' || (!IsVowel(input[i + 1]) && input[i + 1] != 'y')))
            {
                output.Append(table["n"]);
                i += i + 1 < input.Length && input[i + 1] == '\'' ? 2 : 1;
                continue;
            }

            var matched = false;
            for (var len = Math.Min(3, input.Length - i); len >= 1; len--)
            {
                var candidate = input.Substring(i, len);
                if (!table.TryGetValue(candidate, out var kana)) continue;
                output.Append(kana);
                i += len;
                matched = true;
                break;
            }

            if (!matched)
            {
                // Unrecognized character (punctuation, digit, already-kana, ...): pass through unchanged.
                output.Append(input[i]);
                i++;
            }
        }

        return output.ToString();
    }

    private static bool IsVowel(char c) => c is 'a' or 'i' or 'u' or 'e' or 'o';
    private static bool IsConsonant(char c) => char.IsLetter(c) && !IsVowel(c) && c != 'n';

    private static readonly IReadOnlyDictionary<string, string> Hiragana = BuildTable(katakana: false);
    private static readonly IReadOnlyDictionary<string, string> Katakana = BuildTable(katakana: true);

    private static Dictionary<string, string> BuildTable(bool katakana)
    {
        // (romaji, hiragana) pairs. Katakana forms are derived by shifting each
        // hiragana codepoint into the katakana block (they're laid out in
        // Unicode at a fixed +0x60 offset from their hiragana counterparts).
        string[][] rows =
        [
            ["a", "あ"], ["i", "い"], ["u", "う"], ["e", "え"], ["o", "お"],
            ["ka", "か"], ["ki", "き"], ["ku", "く"], ["ke", "け"], ["ko", "こ"],
            ["ga", "が"], ["gi", "ぎ"], ["gu", "ぐ"], ["ge", "げ"], ["go", "ご"],
            ["sa", "さ"], ["si", "し"], ["shi", "し"], ["su", "す"], ["se", "せ"], ["so", "そ"],
            ["za", "ざ"], ["zi", "じ"], ["ji", "じ"], ["zu", "ず"], ["ze", "ぜ"], ["zo", "ぞ"],
            ["ta", "た"], ["ti", "ち"], ["chi", "ち"], ["tu", "つ"], ["tsu", "つ"], ["te", "て"], ["to", "と"],
            ["da", "だ"], ["di", "ぢ"], ["du", "づ"], ["de", "で"], ["do", "ど"],
            ["na", "な"], ["ni", "に"], ["nu", "ぬ"], ["ne", "ね"], ["no", "の"],
            ["ha", "は"], ["hi", "ひ"], ["hu", "ふ"], ["fu", "ふ"], ["he", "へ"], ["ho", "ほ"],
            ["ba", "ば"], ["bi", "び"], ["bu", "ぶ"], ["be", "べ"], ["bo", "ぼ"],
            ["pa", "ぱ"], ["pi", "ぴ"], ["pu", "ぷ"], ["pe", "ぺ"], ["po", "ぽ"],
            ["ma", "ま"], ["mi", "み"], ["mu", "む"], ["me", "め"], ["mo", "も"],
            ["ya", "や"], ["yu", "ゆ"], ["yo", "よ"],
            ["ra", "ら"], ["ri", "り"], ["ru", "る"], ["re", "れ"], ["ro", "ろ"],
            ["wa", "わ"], ["wo", "を"],
            ["n", "ん"],
            ["xtu", "っ"], ["ltu", "っ"],
            // Small-y digraphs (youon), plus their "sh/ch/j" irregular-consonant equivalents.
            ["kya", "きゃ"], ["kyu", "きゅ"], ["kyo", "きょ"],
            ["gya", "ぎゃ"], ["gyu", "ぎゅ"], ["gyo", "ぎょ"],
            ["sya", "しゃ"], ["sha", "しゃ"], ["syu", "しゅ"], ["shu", "しゅ"], ["syo", "しょ"], ["sho", "しょ"],
            ["zya", "じゃ"], ["ja", "じゃ"], ["zyu", "じゅ"], ["ju", "じゅ"], ["zyo", "じょ"], ["jo", "じょ"],
            ["tya", "ちゃ"], ["cha", "ちゃ"], ["tyu", "ちゅ"], ["chu", "ちゅ"], ["tyo", "ちょ"], ["cho", "ちょ"],
            ["nya", "にゃ"], ["nyu", "にゅ"], ["nyo", "にょ"],
            ["hya", "ひゃ"], ["hyu", "ひゅ"], ["hyo", "ひょ"],
            ["bya", "びゃ"], ["byu", "びゅ"], ["byo", "びょ"],
            ["pya", "ぴゃ"], ["pyu", "ぴゅ"], ["pyo", "ぴょ"],
            ["mya", "みゃ"], ["myu", "みゅ"], ["myo", "みょ"],
            ["rya", "りゃ"], ["ryu", "りゅ"], ["ryo", "りょ"],
        ];

        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
            table[row[0]] = katakana ? HiraganaToKatakana(row[1]) : row[1];
        return table;
    }

    private static string HiraganaToKatakana(string hiragana)
    {
        var chars = hiragana.Select(c => c is >= 'あ' and <= 'ん' ? (char)(c + 0x60) : c);
        return new string(chars.ToArray());
    }
}
