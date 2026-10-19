using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class HandwritingSheetPage : ContentPage
{
    private readonly HandwritingViewModel viewModel;

    /// <summary>Set by the caller (SearchViewModel.OpenDrawAsync) before this
    /// page is pushed modally; invoked when a candidate is tapped, right
    /// before the sheet closes itself.</summary>
    public Action<string>? OnCharacterPicked { get; set; }

    public HandwritingSheetPage(HandwritingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;

        // One subscriber, one place that decides what "pick" means: forward
        // to whoever opened the sheet, then close. Previously this was split
        // across two separate assignments to the same Action property, and
        // the second one silently clobbered the first (see HandwritingViewModel).
        viewModel.CandidatePicked += OnCandidatePicked;
    }

    private void OnCandidatePicked(string character)
    {
        OnCharacterPicked?.Invoke(character);
        _ = CloseAsync();
    }

    // Portrait canvas height; in landscape it shrinks to fit what's left of the
    // page after the header, the button row, the sheet's bottom padding and the
    // system bars (the reported page height includes them, ~55dp on the test
    // phone, but the sheet can't draw under them).
    private const double PortraitCanvasHeight = 230;
    private const double LandscapeChromeHeight = 210;
    private const double MinCanvasHeight = 120;

    private bool? isLandscape;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        var landscape = width > height;
        if (landscape != isLandscape)
        {
            isLandscape = landscape;
            Body.ColumnDefinitions = landscape
                ? new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }
                : new ColumnDefinitionCollection { new(GridLength.Star) };
            MatchesRule.IsVisible = !landscape;
            Grid.SetColumn(MatchesSection, landscape ? 1 : 0);
            Grid.SetRow(MatchesSection, landscape ? 0 : 2);
        }

        CanvasHost.HeightRequest = landscape
            ? Math.Clamp(height - LandscapeChromeHeight, MinCanvasHeight, PortraitCanvasHeight)
            : PortraitCanvasHeight;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DrawPad.Clear();
        viewModel.Reset();
    }

    private void OnStrokeCompleted(object? sender, int strokeCount) =>
        _ = viewModel.RecognizeAsync(DrawPad.Strokes);

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        DrawPad.Undo();
        _ = viewModel.RecognizeAsync(DrawPad.Strokes);
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        DrawPad.Clear();
        _ = viewModel.RecognizeAsync(DrawPad.Strokes);
    }

    private async void OnDoneClicked(object? sender, EventArgs e) => await CloseAsync();

    private async Task CloseAsync() => await Navigation.PopModalAsync();
}
