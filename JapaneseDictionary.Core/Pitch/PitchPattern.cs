namespace JapaneseDictionary.Core.Pitch;

/// <summary>
/// The high/low pitch of each mora of a word, plus the pitch of a particle that
/// follows it — the particle is what tells flat words (heiban, particle stays
/// high) from final-accented ones (odaka, particle drops).
/// </summary>
public static class PitchPattern
{
    /// <summary>Pitch per mora followed by one entry for the particle (true = high).
    /// <paramref name="accent"/> is the downstep position: 0 = flat (heiban), 1 = high
    /// on the first mora only, n = high up to and including mora n and low after.
    /// Returns <paramref name="moraCount"/> + 1 values.</summary>
    public static bool[] Compute(int moraCount, int accent)
    {
        if (moraCount < 0) throw new ArgumentOutOfRangeException(nameof(moraCount));
        if (accent < 0 || accent > moraCount) throw new ArgumentOutOfRangeException(nameof(accent));

        var pattern = new bool[moraCount + 1];
        for (var i = 0; i < moraCount; i++)
        {
            var position = i + 1; // 1-based mora number
            pattern[i] = accent switch
            {
                0 => position > 1,
                1 => position == 1,
                _ => position > 1 && position <= accent,
            };
        }

        // Particle: high only after a flat word.
        pattern[moraCount] = accent == 0;
        return pattern;
    }

    /// <summary>The pitch pattern as H/L letters, particle last ("LHH" + "H" for flat 3-mora).</summary>
    public static string ToLetters(bool[] pattern) =>
        string.Concat(pattern.Select(h => h ? 'H' : 'L'));
}
