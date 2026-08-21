namespace JapaneseDictionary.Core.Models;

/// <summary>
/// Conjugation class of a word. Drives which ending rules the deinflection
/// engine tries when a search query doesn't match any dictionary headword
/// directly. Mirrors the standard Japanese verb/adjective classification
/// (godan verbs grouped by their dictionary-form ending row, plus the usual
/// irregulars) rather than any particular dictionary's own tagging scheme.
/// </summary>
public enum WordClass
{
    Other = 0,

    /// <summary>Ichidan verb, e.g. 食べる, 見る.</summary>
    IchidanVerb,

    /// <summary>Godan verb ending in -u, e.g. 買う.</summary>
    GodanVerbU,

    /// <summary>Godan verb ending in -ku, e.g. 書く.</summary>
    GodanVerbKu,

    /// <summary>Godan verb ending in -gu, e.g. 泳ぐ.</summary>
    GodanVerbGu,

    /// <summary>Godan verb ending in -su, e.g. 話す.</summary>
    GodanVerbSu,

    /// <summary>Godan verb ending in -tsu, e.g. 待つ.</summary>
    GodanVerbTsu,

    /// <summary>Godan verb ending in -nu, e.g. 死ぬ.</summary>
    GodanVerbNu,

    /// <summary>Godan verb ending in -bu, e.g. 遊ぶ.</summary>
    GodanVerbBu,

    /// <summary>Godan verb ending in -mu, e.g. 読む.</summary>
    GodanVerbMu,

    /// <summary>Godan verb ending in -ru, e.g. 帰る.</summary>
    GodanVerbRu,

    /// <summary>行く/往く class: godan -ku verb with an irregular te/ta sound change (行って, not 行いて).</summary>
    GodanVerbIkuYuku,

    /// <summary>する and する-compounds (勉強する, 愛する, etc.), irregular conjugation.</summary>
    SuruVerb,

    /// <summary>来る (来て, 来た, 来ない...), irregular conjugation with reading changes.</summary>
    KuruVerb,

    /// <summary>i-adjective, e.g. 高い, 楽しい.</summary>
    IAdjective,

    /// <summary>na-adjective, e.g. 静か, 元気. Conjugates through the copula (だ/です), not the word itself.</summary>
    NaAdjective,
}
