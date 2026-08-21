namespace JapaneseDictionary.Core.Models;

/// <summary>
/// One Japanese/English sentence pair from Tatoeba, optionally linked to the
/// dictionary entries it illustrates (via Tatoeba's Japanese Indices export).
/// </summary>
public sealed record ExampleSentence(
    long Id,
    string Japanese,
    string English,
    IReadOnlyList<int> LinkedEntryIds);
