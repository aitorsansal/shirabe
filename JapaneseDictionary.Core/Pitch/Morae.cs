namespace JapaneseDictionary.Core.Pitch;

/// <summary>
/// Splits a kana reading into morae, the unit pitch accent is counted in.
/// Small ya/yu/yo and small vowels join the preceding mora (きゃ, ファ); the
/// sokuon (っ), the moraic nasal (ん) and the long-vowel mark (ー) each count as
/// a mora of their own.
/// </summary>
public static class Morae
{
    private const string JoiningSmallKana = "ゃゅょぁぃぅぇぉゎャュョァィゥェォヮ";

    public static IReadOnlyList<string> Split(string reading)
    {
        var morae = new List<string>();
        foreach (var c in reading)
        {
            if (morae.Count > 0 && JoiningSmallKana.Contains(c))
                morae[^1] += c;
            else
                morae.Add(c.ToString());
        }

        return morae;
    }

    public static int Count(string reading) => Split(reading).Count;
}
