namespace JapaneseDictionary.Core.Recognition;

/// <summary>Whole-character recognition over a set of captured strokes.
/// Engine-agnostic on purpose — the concrete implementation (ONNX classifier)
/// lives entirely behind this seam.</summary>
public interface IDrawRecognizer
{
    /// <summary>Ranks candidate characters for the given strokes, most likely
    /// first. Strokes are raw on-screen points — not pre-scaled to any
    /// particular coordinate space. The recognizer owns whatever
    /// rasterizing/normalizing its own engine needs.</summary>
    Task<IReadOnlyList<RecognitionCandidate>> RecognizeAsync(
        IReadOnlyList<IReadOnlyList<DrawPoint>> strokes,
        CancellationToken cancellationToken = default);
}

/// <summary>One ranked recognition result. Score is only meaningful relative
/// to other candidates from the same call.</summary>
public readonly record struct RecognitionCandidate(string Character, float Score);
