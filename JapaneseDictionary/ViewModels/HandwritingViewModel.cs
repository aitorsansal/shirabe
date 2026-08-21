using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Recognition;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

public sealed record CandidateTile(string Character, int StrokeCount, float Score);

public partial class HandwritingViewModel : ObservableObject
{
    private const int MaxCandidates = 10;

    private readonly AppDataContext _data;

    public HandwritingViewModel(AppDataContext data)
    {
        _data = data;
    }

    [ObservableProperty]
    private int strokeCount;

    [ObservableProperty]
    private bool isRecognizing;

    public ObservableCollection<CandidateTile> Candidates { get; } = [];

    public bool CanvasEmpty => StrokeCount == 0;
    public string CandidateNote => StrokeCount == 0 ? "draw to search" : $"top {Candidates.Count}";

    /// <summary>Raised when the user taps a candidate. Only
    /// <see cref="Views.HandwritingSheetPage"/> subscribes to this — it owns
    /// forwarding the pick to whoever opened the sheet and then closing it.
    /// (This used to be a single externally-settable Action property, but a
    /// second caller assigning it would silently overwrite the page's own
    /// "close on pick" handler — see the fix in HandwritingSheetPage.)</summary>
    public event Action<string>? CandidatePicked;

    public async Task RecognizeAsync(IReadOnlyList<IReadOnlyList<PointF>> strokes)
    {
        StrokeCount = strokes.Count;
        OnPropertyChanged(nameof(CanvasEmpty));

        if (strokes.Count == 0)
        {
            Candidates.Clear();
            OnPropertyChanged(nameof(CandidateNote));
            return;
        }

        IsRecognizing = true;
        await _data.EnsureInitializedAsync();

        IReadOnlyList<RecognitionCandidate> results;
        try
        {
            results = await _data.Recognizer.RecognizeAsync(strokes);
        }
        finally
        {
            IsRecognizing = false;
        }

        Candidates.Clear();
        foreach (var candidate in results.Take(MaxCandidates))
        {
            var info = _data.DictionaryRepository.GetKanjiInfo(candidate.Character);
            Candidates.Add(new CandidateTile(candidate.Character, info?.StrokeCount ?? 0, candidate.Score));
        }

        OnPropertyChanged(nameof(CandidateNote));
    }

    [RelayCommand]
    private void PickCandidate(string character) => CandidatePicked?.Invoke(character);

    public void Reset()
    {
        StrokeCount = 0;
        Candidates.Clear();
        OnPropertyChanged(nameof(CanvasEmpty));
        OnPropertyChanged(nameof(CandidateNote));
    }
}
