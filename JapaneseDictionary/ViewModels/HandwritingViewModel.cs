using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Core.Recognition;
using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;

namespace JapaneseDictionary.ViewModels;

public sealed record CandidateTile(string Character, int StrokeCount, float Score);

public partial class HandwritingViewModel : ObservableObject
{
    private const int MaxCandidates = 10;

    private readonly AppDataContext data;

    public HandwritingViewModel(AppDataContext data)
    {
        this.data = data;
    }

    [ObservableProperty]
    private int strokeCount;

    [ObservableProperty]
    private bool isRecognizing;

    public ObservableCollection<CandidateTile> Candidates { get; } = [];

    public bool CanvasEmpty => StrokeCount == 0;
    public string CandidateNote => StrokeCount == 0
        ? AppResources.Hand_NoteEmpty
        : UiText.Format(AppResources.Hand_NoteTop, Candidates.Count);

    private const string AppendModeKey = "handwriting_append_mode";

    /// <summary>True: a picked character is added to the query and the sheet stays
    /// open for the next one. False (default, "Replace"): the pick becomes the whole
    /// query and the sheet closes. Persisted across sessions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReplaceMode))]
    private bool isAppendMode = Preferences.Default.Get(AppendModeKey, false);

    public bool IsReplaceMode => !IsAppendMode;

    partial void OnIsAppendModeChanged(bool value) => Preferences.Default.Set(AppendModeKey, value);

    /// <summary>The caller's query as it is being built, mirrored by the page so the
    /// Append-mode line can show it. The page refreshes it; it is display state only.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuery))]
    private string composedQuery = "";

    public bool HasQuery => ComposedQuery.Length > 0;

    [RelayCommand]
    private void SetMode(string mode) => IsAppendMode = mode == "Append";

    /// <summary>Raised when the user taps a candidate. Only
    /// <see cref="Views.HandwritingSheetPage"/> subscribes to this — it owns
    /// deciding what a pick means (replace or append to the query, via the
    /// getter/setter whoever opened the sheet handed it) and whether the sheet
    /// then closes. (This used to be a single externally-settable Action
    /// property, but a second caller assigning it would silently overwrite the
    /// page's own handler — see the fix in HandwritingSheetPage.)</summary>
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
        await data.EnsureInitializedAsync();

        IReadOnlyList<RecognitionCandidate> results;
        try
        {
            results = await data.Recognizer.RecognizeAsync(strokes);
        }
        finally
        {
            IsRecognizing = false;
        }

        Candidates.Clear();
        foreach (var candidate in results.Take(MaxCandidates))
        {
            var info = data.DictionaryRepository.GetKanjiInfo(candidate.Character);
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
