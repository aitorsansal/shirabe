namespace JapaneseDictionary.Core.Models;

/// <summary>Maps JMdict's own short part-of-speech tags to <see cref="WordClass"/>.</summary>
public static class WordClassMap
{
    public static WordClass FromJmdictTag(string tag) => tag switch
    {
        "v1" or "v1-s" or "vz" => WordClass.IchidanVerb,
        "v5u" or "v5u-s" => WordClass.GodanVerbU,
        "v5k" => WordClass.GodanVerbKu,
        "v5k-s" => WordClass.GodanVerbIkuYuku,
        "v5g" => WordClass.GodanVerbGu,
        "v5s" => WordClass.GodanVerbSu,
        "v5t" => WordClass.GodanVerbTsu,
        "v5n" => WordClass.GodanVerbNu,
        "v5b" => WordClass.GodanVerbBu,
        "v5m" => WordClass.GodanVerbMu,
        "v5r" or "v5r-i" or "v5aru" or "v5uru" => WordClass.GodanVerbRu,
        "vs" or "vs-c" or "vs-i" or "vs-s" => WordClass.SuruVerb,
        "vk" => WordClass.KuruVerb,
        "adj-i" or "adj-ix" => WordClass.IAdjective,
        "adj-na" or "adj-nari" => WordClass.NaAdjective,
        _ => WordClass.Other,
    };
}
