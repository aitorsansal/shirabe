using Android.Content;

namespace JapaneseDictionary;

/// <summary>Extras and receiver rules shared with Nihongo No Practise, per
/// docs/COMPANION_CONTRACT.md. Any app can send these intents, so everything
/// read here is untrusted input.</summary>
public static class CompanionIntents
{
    /// <summary>Contract version a request was written for; missing means 1.</summary>
    public const string VersionExtra = "com.aitorsansal.companion.V";

    /// <summary>Kana reading of a Look Up query (action 1).</summary>
    public const string ReadingExtra = "com.aitorsansal.companion.READING";

    /// <summary>Highest contract version this build understands.</summary>
    public const int SupportedVersion = 1;

    private const int MaxReadingLength = 32;

    /// <summary>True if the request asks for a newer contract than this build has:
    /// the receiver then only shows the update message.</summary>
    public static bool NeedsNewerApp(Intent? intent) =>
        intent?.GetIntExtra(VersionExtra, 1) > SupportedVersion;

    /// <summary>A relaunch from Recents carries the original intent again; it must
    /// not replay the request.</summary>
    public static bool IsReplayFromRecents(Intent? intent) =>
        intent != null && intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory);

    /// <summary>The reading extra if it's plain kana of a sane length, else null.
    /// Anything else (wildcards, markup, Latin text) would only send the reading
    /// search somewhere expensive or wrong, so it's ignored.</summary>
    public static string? ReadReading(Intent? intent)
    {
        var reading = intent?.GetStringExtra(ReadingExtra)?.Trim();
        if (string.IsNullOrEmpty(reading) || reading.Length > MaxReadingLength) return null;
        return reading.All(c => c is >= 'ぁ' and <= 'ゟ' or >= '゠' and <= 'ヿ') ? reading : null;
    }
}
