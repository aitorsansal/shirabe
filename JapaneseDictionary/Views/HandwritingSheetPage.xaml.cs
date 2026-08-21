using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class HandwritingSheetPage : ContentPage
{
    private readonly HandwritingViewModel _viewModel;

    /// <summary>Set by the caller (SearchViewModel.OpenDrawAsync) before this
    /// page is pushed modally; invoked when a candidate is tapped, right
    /// before the sheet closes itself.</summary>
    public Action<string>? OnCharacterPicked { get; set; }

    public HandwritingSheetPage(HandwritingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // One subscriber, one place that decides what "pick" means: forward
        // to whoever opened the sheet, then close. Previously this was split
        // across two separate assignments to the same Action property, and
        // the second one silently clobbered the first (see HandwritingViewModel).
        _viewModel.CandidatePicked += OnCandidatePicked;
    }

    private void OnCandidatePicked(string character)
    {
        OnCharacterPicked?.Invoke(character);
        _ = CloseAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DrawPad.Clear();
        _viewModel.Reset();
    }

    private void OnStrokeCompleted(object? sender, int strokeCount) =>
        _ = _viewModel.RecognizeAsync(DrawPad.Strokes);

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        DrawPad.Undo();
        _ = _viewModel.RecognizeAsync(DrawPad.Strokes);
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        DrawPad.Clear();
        _ = _viewModel.RecognizeAsync(DrawPad.Strokes);
    }

    private async void OnDoneClicked(object? sender, EventArgs e) => await CloseAsync();

    private async Task CloseAsync() => await Navigation.PopModalAsync();
}
