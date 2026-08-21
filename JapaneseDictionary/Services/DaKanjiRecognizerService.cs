using JapaneseDictionary.Core.Recognition;

namespace JapaneseDictionary.Services;

/// <summary>
/// MAUI-side adapter for <see cref="DaKanjiRecognizer"/>: owns loading the
/// bundled model asset (which needs <see cref="FileSystem"/>, a MAUI-only
/// API) and converts <see cref="PointF"/> strokes from
/// <see cref="Controls.KanjiDrawPad"/> to the platform-agnostic
/// <see cref="DrawPoint"/> the Core recognizer expects.
/// </summary>
public sealed class DaKanjiRecognizerService : IAsyncDisposable
{
    private const string ModelAsset = "Models/char_classifier.onnx";
    private const string LabelsAsset = "Models/char_classifier_labels.txt";

    private readonly Lazy<Task<DaKanjiRecognizer>> _loaded = new(LoadAsync);

    public async Task<IReadOnlyList<RecognitionCandidate>> RecognizeAsync(
        IReadOnlyList<IReadOnlyList<PointF>> strokes,
        CancellationToken cancellationToken = default)
    {
        var recognizer = await _loaded.Value;
        var converted = strokes
            .Select(stroke => (IReadOnlyList<DrawPoint>)stroke.Select(p => new DrawPoint(p.X, p.Y)).ToList())
            .ToList();

        return await recognizer.RecognizeAsync(converted, cancellationToken);
    }

    private static async Task<DaKanjiRecognizer> LoadAsync()
    {
        using var modelStream = await FileSystem.OpenAppPackageFileAsync(ModelAsset);
        using var modelBytes = new MemoryStream();
        await modelStream.CopyToAsync(modelBytes);

        using var labelsStream = await FileSystem.OpenAppPackageFileAsync(LabelsAsset);
        using var reader = new StreamReader(labelsStream);
        var labels = await reader.ReadToEndAsync();

        return new DaKanjiRecognizer(modelBytes.ToArray(), labels);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_loaded.IsValueCreated) return;
        var recognizer = await _loaded.Value;
        recognizer.Dispose();
    }
}
