using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace JapaneseDictionary.Core.Recognition;

/// <summary>
/// <see cref="IDrawRecognizer"/> over the DaKanji ONNX classifier
/// (6507-class kanji/kana recognizer). Model conventions confirmed against
/// the real model graph:
/// <list type="bullet">
/// <item>Input tensor: "image", float32, (batch, channel, height, width),
/// internally resized to a fixed 64x64 by a bilinear Resize node baked into
/// the graph.</item>
/// <item>Normalization: the graph divides every pixel by the image's own max
/// value — no fixed /255 or -1..1 scaling needed on the caller's side.</item>
/// <item>Ink polarity: light ink (255) on a dark background (0).</item>
/// <item>Output: "probs", already softmax'd.</item>
/// <item>Labels: one unbroken string, output index i maps to labels[i].</item>
/// </list>
/// </summary>
public sealed class DaKanjiRecognizer : IDrawRecognizer, IDisposable
{
    private const int InputSize = 64;
    private const string InputName = "image";
    private const string OutputName = "probs";

    // Radius of the "brush" stamped along each stroke, relative to canvas size.
    private const float BrushRadiusRatio = 1f / 24f;
    private const float MarginRatio = 0.12f;

    private readonly InferenceSession _session;
    private readonly string _labels;

    /// <param name="modelBytes">The raw .onnx model file contents.</param>
    /// <param name="labels">The label string: one character per output class index.</param>
    public DaKanjiRecognizer(byte[] modelBytes, string labels)
    {
        _session = new InferenceSession(modelBytes);
        _labels = labels;
    }

    public Task<IReadOnlyList<RecognitionCandidate>> RecognizeAsync(
        IReadOnlyList<IReadOnlyList<DrawPoint>> strokes,
        CancellationToken cancellationToken = default)
    {
        var input = Rasterize(strokes, InputSize);
        cancellationToken.ThrowIfCancellationRequested();

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(InputName, input) };
        using var results = _session.Run(inputs);
        var probs = results.First(r => r.Name == OutputName).AsEnumerable<float>().ToArray();

        var candidates = new RecognitionCandidate[probs.Length];
        for (var i = 0; i < probs.Length; i++)
            candidates[i] = new RecognitionCandidate(_labels[i].ToString(), probs[i]);

        IReadOnlyList<RecognitionCandidate> ranked = candidates.OrderByDescending(c => c.Score).ToList();
        return Task.FromResult(ranked);
    }

    public void Dispose() => _session.Dispose();

    /// <summary>Pure-software rasterizer: no platform canvas/bitmap dependency,
    /// deterministic, directly testable. Strokes are centered and scaled to
    /// fit the target square with a margin, preserving their relative
    /// positions/proportions to each other (the bounding box is computed
    /// across all strokes together, not per-stroke).</summary>
    internal static DenseTensor<float> Rasterize(IReadOnlyList<IReadOnlyList<DrawPoint>> strokes, int size)
    {
        var buffer = new float[size * size]; // 0f everywhere = dark background

        var box = BoundingBox(strokes);
        var boxSize = MathF.Max(box.MaxX - box.MinX, box.MaxY - box.MinY);
        if (boxSize < 1f) boxSize = 1f; // degenerate guard: a single dot / empty strokes

        var margin = size * MarginRatio;
        var scale = (size - 2 * margin) / boxSize;
        var cx = (box.MinX + box.MaxX) / 2f;
        var cy = (box.MinY + box.MaxY) / 2f;
        var radius = MathF.Max(1f, size * BrushRadiusRatio);

        DrawPoint ToCanvas(DrawPoint p) => new(
            size / 2f + (p.X - cx) * scale,
            size / 2f + (p.Y - cy) * scale);

        foreach (var stroke in strokes)
        {
            DrawPoint? previous = null;
            foreach (var rawPoint in stroke)
            {
                var point = ToCanvas(rawPoint);
                if (previous is { } prev) StampLine(buffer, size, prev, point, radius);
                StampDisk(buffer, size, point, radius);
                previous = point;
            }
        }

        return new DenseTensor<float>(buffer, [1, 1, size, size]);
    }

    private static void StampLine(float[] buffer, int size, DrawPoint a, DrawPoint b, float radius)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)(distance / MathF.Max(1f, radius * 0.5f)));

        for (var s = 1; s < steps; s++)
        {
            var t = (float)s / steps;
            StampDisk(buffer, size, new DrawPoint(a.X + dx * t, a.Y + dy * t), radius);
        }
    }

    private static void StampDisk(float[] buffer, int size, DrawPoint center, float radius)
    {
        var minPx = Math.Max(0, (int)(center.X - radius));
        var maxPx = Math.Min(size - 1, (int)(center.X + radius));
        var minPy = Math.Max(0, (int)(center.Y - radius));
        var maxPy = Math.Min(size - 1, (int)(center.Y + radius));
        var radiusSq = radius * radius;

        for (var py = minPy; py <= maxPy; py++)
        for (var px = minPx; px <= maxPx; px++)
        {
            var ddx = px + 0.5f - center.X;
            var ddy = py + 0.5f - center.Y;
            if (ddx * ddx + ddy * ddy <= radiusSq)
                buffer[py * size + px] = 255f; // light ink
        }
    }

    private static (float MinX, float MinY, float MaxX, float MaxY) BoundingBox(
        IReadOnlyList<IReadOnlyList<DrawPoint>> strokes)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var stroke in strokes)
        foreach (var p in stroke)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return (minX, minY, maxX, maxY);
    }
}
