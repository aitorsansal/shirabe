namespace JapaneseDictionary.Core.Search;

/// <summary>
/// Tiebreak for "which of these equally good matches is the everyday word":
/// the KANJIDIC2 frequency rank of a headword's rarest kanji (lower = more
/// everyday). JMdict's per-word frequency tags aren't in the schema, and kanji
/// frequency is a decent proxy. Kana-only headwords sit between common and
/// rare kanji, since they're as often everyday words as obscure ones.
/// </summary>
internal static class KanjiRarity
{
    private const int KanaOnly = 2500;
    private const int Unranked = 3000;

    public static int Of(string headword, IReadOnlyDictionary<char, int> kanjiFrequencyRanks)
    {
        var rarity = -1;
        foreach (var c in headword)
        {
            if (c is < '一' or > '鿿') continue;
            rarity = Math.Max(rarity, kanjiFrequencyRanks.GetValueOrDefault(c, Unranked));
        }

        return rarity < 0 ? KanaOnly : rarity;
    }
}
