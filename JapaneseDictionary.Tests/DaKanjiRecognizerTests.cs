using JapaneseDictionary.Core.Recognition;

namespace JapaneseDictionary.Tests;

public class DaKanjiRecognizerTests : IDisposable
{
    private const string ModelPath = "../../../../JapaneseDictionary/Resources/Raw/Models/char_classifier.onnx";
    private const string LabelsPath = "../../../../JapaneseDictionary/Resources/Raw/Models/char_classifier_labels.txt";

    private readonly DaKanjiRecognizer? _recognizer;

    public DaKanjiRecognizerTests()
    {
        if (File.Exists(ModelPath) && File.Exists(LabelsPath))
            _recognizer = new DaKanjiRecognizer(File.ReadAllBytes(ModelPath), File.ReadAllText(LabelsPath));
    }

    public void Dispose() => _recognizer?.Dispose();

    [SkippableFact]
    public async Task RecognizeAsync_ReturnsOneCandidatePerLabel_SortedByScoreDescending()
    {
        Skip.If(_recognizer == null, "model assets not present");

        // A simple cross-like scribble — not expected to match any specific
        // character reliably, just exercising the full pipeline end-to-end.
        List<IReadOnlyList<DrawPoint>> strokes =
        [
            [new DrawPoint(10, 32), new DrawPoint(54, 32)],
            [new DrawPoint(32, 10), new DrawPoint(32, 54)],
        ];

        var results = await _recognizer!.RecognizeAsync(strokes);

        Assert.Equal(File.ReadAllText(LabelsPath).Length, results.Count);
        for (var i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Score >= results[i].Score, "results must be sorted by descending score");
    }

    [Fact]
    public void Rasterize_EmptyStrokes_DoesNotThrow()
    {
        var tensor = DaKanjiRecognizer.Rasterize([], 64);
        Assert.Equal(64 * 64, tensor.Length);
    }

    [Fact]
    public void Rasterize_SinglePoint_CentersItOnCanvas()
    {
        List<IReadOnlyList<DrawPoint>> strokes = [[new DrawPoint(5, 5)]];
        var tensor = DaKanjiRecognizer.Rasterize(strokes, 64);

        // The center pixels should have ink (255f); the far corners should not.
        Assert.Equal(255f, tensor[0, 0, 32, 32]);
        Assert.Equal(0f, tensor[0, 0, 0, 0]);
    }
}
