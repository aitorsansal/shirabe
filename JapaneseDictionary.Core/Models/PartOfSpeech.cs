namespace JapaneseDictionary.Core.Models;

/// <summary>The broad families JMdict's ~80 part-of-speech tags fall into, so
/// the UI can color-code them without listing every tag.</summary>
public enum PartOfSpeechGroup
{
    Noun,
    Verb,
    Adjective,
    Adverb,
    Other,
}

public static class PartOfSpeech
{
    /// <summary>The family of a JMdict part-of-speech tag ("v5u", "adj-no", "n-suf").
    /// Verb tags include transitivity (vt, vi) so they share their verb's color.
    /// Anything unrecognized, including expressions, particles and affixes, is Other.</summary>
    public static PartOfSpeechGroup GroupOf(string tag) => tag switch
    {
        "n" or "pn" or "num" or "ctr" => PartOfSpeechGroup.Noun,
        "adv" or "adv-to" => PartOfSpeechGroup.Adverb,
        "aux-v" => PartOfSpeechGroup.Verb,
        _ when tag.StartsWith("n-", StringComparison.Ordinal) => PartOfSpeechGroup.Noun,
        _ when tag.StartsWith("adj-", StringComparison.Ordinal) => PartOfSpeechGroup.Adjective,
        _ when tag.StartsWith('v') => PartOfSpeechGroup.Verb,
        _ => PartOfSpeechGroup.Other,
    };
}
