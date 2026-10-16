using Android.Speech.Tts;

using AndroidTts = Android.Speech.Tts.TextToSpeech;

namespace JapaneseDictionary.Services;

/// <summary>A TTS engine that can speak Japanese, for the Settings picker.
/// <see cref="Package"/> is the engine's package name.</summary>
public sealed record TtsEngineOption(string Package, string Label);

/// <summary>
/// Speaks Japanese with the device's TTS engine, built on the native Android
/// API rather than MAUI's TextToSpeech wrapper because the wrapper always uses
/// the system default engine and can't pick one.
///
/// That matters: the system default is often a third-party or OEM engine
/// (VoxSherpa, Samsung, Xiaomi) chosen for some other language, and its
/// Japanese can be far worse than Google's. So by default ("automatic") this
/// prefers Google's engine when it has Japanese, falling back to the system
/// default; the user can pin a specific engine in Settings instead.
///
/// The engine is bound lazily on first use and kept; a new <see cref="SpeakAsync"/>
/// call cuts off the utterance still playing. Android-only, like the app.
/// </summary>
public sealed class JapaneseTtsService
{
    public const string GoogleEnginePackage = "com.google.android.tts";

    /// <summary>Preference key: an engine package name, or empty for automatic.</summary>
    private const string PreferenceKey = "tts_engine";

    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(5);
    private static readonly Java.Util.Locale Japanese = new("ja", "JP");

    private readonly SemaphoreSlim gate = new(1, 1);
    private AndroidTts? tts;
    private bool resolved;
    private string? currentUtteranceId;
    private TaskCompletionSource? speaking;

    /// <summary>The engine package the user pinned, or empty for automatic.</summary>
    public string PreferredEngine
    {
        get => Preferences.Default.Get(PreferenceKey, "");
        set
        {
            Preferences.Default.Set(PreferenceKey, value);
            Invalidate();
        }
    }

    public async Task<bool> IsAvailableAsync() => await EnsureEngineAsync() != null;

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (await EnsureEngineAsync() is not { } engine) return;

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref speaking, done)?.TrySetResult();

        var utteranceId = Guid.NewGuid().ToString("N");
        currentUtteranceId = utteranceId;
        if (engine.Speak(text, QueueMode.Flush, null, utteranceId) != OperationResult.Success)
        {
            done.TrySetResult();
            return;
        }

        using (ct.Register(Stop))
            await done.Task;
    }

    public void Stop()
    {
        tts?.Stop();
        Interlocked.Exchange(ref speaking, null)?.TrySetResult();
    }

    /// <summary>Drop the bound engine so the next call re-resolves it — after
    /// the user changes the engine here, or may have installed a voice in the
    /// system settings.</summary>
    public void Invalidate()
    {
        Stop();
        var old = tts;
        tts = null;
        resolved = false;
        old?.Shutdown();
    }

    /// <summary>Every installed engine that can speak Japanese. Binds each one
    /// briefly to ask, so only call it from Settings, not per word.</summary>
    public async Task<IReadOnlyList<TtsEngineOption>> GetJapaneseEnginesAsync()
    {
        var probe = await CreateAsync(null);
        if (probe == null) return [];
        var engines = (probe.Engines ?? []).Select(e => new TtsEngineOption(e.Name ?? "", e.Label ?? e.Name ?? "")).Where(e => e.Package.Length > 0).ToList();
        probe.Shutdown();

        var result = new List<TtsEngineOption>();
        foreach (var engine in engines)
        {
            var candidate = await CreateAsync(engine.Package);
            if (candidate == null) continue;
            if (SupportsJapanese(candidate)) result.Add(engine);
            candidate.Shutdown();
        }

        return result;
    }

    private async Task<AndroidTts?> EnsureEngineAsync()
    {
        if (resolved) return tts;
        await gate.WaitAsync();
        try
        {
            if (resolved) return tts;

            foreach (var package in CandidateEngines())
            {
                var candidate = await CreateAsync(package);
                if (candidate == null) continue;
                if (SupportsJapanese(candidate))
                {
                    candidate.SetLanguage(Japanese);
                    candidate.SetOnUtteranceProgressListener(new ProgressListener(OnUtteranceFinished));
                    tts = candidate;
                    break;
                }

                candidate.Shutdown();
            }

            resolved = true;
            return tts;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The pinned engine first (if still installed), then Google,
    /// then the system default (null). Android silently swaps in the default
    /// engine for a package that isn't installed, so missing ones are skipped
    /// rather than tried.</summary>
    private IEnumerable<string?> CandidateEngines()
    {
        var preferred = PreferredEngine;
        if (preferred.Length > 0 && IsInstalled(preferred)) yield return preferred;
        if (preferred != GoogleEnginePackage && IsInstalled(GoogleEnginePackage)) yield return GoogleEnginePackage;
        yield return null;
    }

    private void OnUtteranceFinished(string? utteranceId)
    {
        if (utteranceId != currentUtteranceId) return;
        Interlocked.Exchange(ref speaking, null)?.TrySetResult();
    }

    private static bool SupportsJapanese(AndroidTts engine) =>
        engine.IsLanguageAvailable(Japanese) >= LanguageAvailableResult.Available;

    private static bool IsInstalled(string package)
    {
        try
        {
            Platform.AppContext.PackageManager!.GetPackageInfo(package, 0);
            return true;
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Binds an engine (null = system default) and waits for it to
    /// initialise. Returns null if it fails or doesn't answer in time.</summary>
    private static async Task<AndroidTts?> CreateAsync(string? package)
    {
        var listener = new InitListener();
        var engine = await MainThread.InvokeOnMainThreadAsync(() => package == null
            ? new AndroidTts(Platform.AppContext, listener)
            : new AndroidTts(Platform.AppContext, listener, package));

        bool ok;
        try
        {
            ok = await listener.Initialized.WaitAsync(InitTimeout);
        }
        catch (TimeoutException)
        {
            ok = false;
        }

        if (ok) return engine;
        engine.Shutdown();
        return null;
    }

    private sealed class InitListener : Java.Lang.Object, AndroidTts.IOnInitListener
    {
        private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> Initialized => initialized.Task;

        public void OnInit(OperationResult status) => initialized.TrySetResult(status == OperationResult.Success);
    }

    private sealed class ProgressListener(Action<string?> finished) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId) { }

        public override void OnDone(string? utteranceId) => finished(utteranceId);

        [Obsolete("Required abstract override; the error-code overload below is the one engines call on API 21+.")]
        public override void OnError(string? utteranceId) => finished(utteranceId);

        public override void OnError(string? utteranceId, TextToSpeechError errorCode) => finished(utteranceId);

        public override void OnStop(string? utteranceId, bool interrupted) => finished(utteranceId);
    }
}
