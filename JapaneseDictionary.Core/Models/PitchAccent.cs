namespace JapaneseDictionary.Core.Models;

/// <summary>
/// Pitch accent of one reading of an entry: the downstep position(s) from
/// Kanjium (0 = flat/heiban; n = the pitch drops after the n-th mora). Several
/// positions mean the word is said either way; the first is the main one.
/// </summary>
public sealed record PitchAccent(string Reading, IReadOnlyList<int> Accents);
