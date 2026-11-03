namespace JapaneseDictionary.Core.Models;

/// <summary>
/// How frequent a kanji spelling or reading is, condensed from JMdict's
/// ke_pri/re_pri markers into one number where lower means more frequent:
/// <list type="bullet">
/// <item>1–48: the word's nfXX band (each band is ~500 words of newspaper frequency, nf01 the most frequent)</item>
/// <item><see cref="CommonUnranked"/>: a "common" marker (news1, ichi1, spec1, spec2, gai1) without an nf band</item>
/// <item><see cref="Secondary"/>: only second-tier markers (news2, ichi2, gai2)</item>
/// <item><see cref="None"/>: no marker at all</item>
/// </list>
/// A plain common/uncommon flag can't order homophones: 紙 (nf03), 髪 (nf06),
/// 加味 (nf15) and 上 as かみ (ichi1 only) are all "common".
/// </summary>
public static class FormPriority
{
    public const int CommonUnranked = 49;
    public const int Secondary = 60;
    public const int None = 100;

    /// <summary>Width of a <see cref="Band"/> in nf steps. Wide enough that words
    /// of similar frequency (紙 nf03, 髪 nf06) tie and fall through to the
    /// example-sentence evidence, narrow enough to keep 加味 (nf15) apart.</summary>
    private const int NfBandWidth = 8;

    private static readonly HashSet<string> CommonMarkers =
        new(StringComparer.Ordinal) { "news1", "ichi1", "spec1", "spec2", "gai1" };

    private static readonly HashSet<string> SecondaryMarkers =
        new(StringComparer.Ordinal) { "news2", "ichi2", "gai2" };

    public static int FromMarkers(IEnumerable<string> markers)
    {
        var best = None;
        foreach (var marker in markers)
        {
            var value =
                marker.Length == 4 && marker.StartsWith("nf", StringComparison.Ordinal) && int.TryParse(marker.AsSpan(2), out var nf) ? nf
                : CommonMarkers.Contains(marker) ? CommonUnranked
                : SecondaryMarkers.Contains(marker) ? Secondary
                : None;
            best = Math.Min(best, value);
        }

        return best;
    }

    /// <summary>Coarse rank for sorting, 0 = most frequent: nf01–08, nf09–16, …,
    /// nf41–48, then common without nf, secondary, none. Not a substitute for the
    /// forms' own IsCommon flag: a form tagged ichi1 and nf30 is common, but only
    /// its nf band survives in the priority value.</summary>
    public static int Band(int priority) => priority switch
    {
        <= 48 => (priority - 1) / NfBandWidth,
        CommonUnranked => 6,
        Secondary => 7,
        _ => 8,
    };
}
