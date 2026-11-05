using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class HandwritingSheetPage : ContentPage
{
    private readonly HandwritingViewModel viewModel;

    private Func<string> getQuery = () => "";
    private Action<string> setQuery = _ => { };

    /// <summary>Hands the sheet the caller's search box (SearchViewModel.OpenDrawAsync),
    /// before the page is pushed modally. The sheet reads the current query to show
    /// and extend it in Append mode, and writes it back on every pick or backspace.</summary>
    public void Attach(Func<string> getQuery, Action<string> setQuery)
    {
        this.getQuery = getQuery;
        this.setQuery = setQuery;
        viewModel.ComposedQuery = getQuery();
    }

    public HandwritingSheetPage(HandwritingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;

        // One subscriber, one place that decides what "pick" means (see
        // HandwritingViewModel.CandidatePicked): Replace sets the query to the
        // character and closes; Append adds it, wipes the canvas for the next
        // character and stays open.
        viewModel.CandidatePicked += OnCandidatePicked;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HandwritingViewModel.IsAppendMode)) ApplyCanvasHeight();
        };
    }

    private void OnCandidatePicked(string character)
    {
        if (!viewModel.IsAppendMode)
        {
            setQuery(character);
            _ = CloseAsync();
            return;
        }

        setQuery(getQuery() + character);
        viewModel.ComposedQuery = getQuery();
        DrawPad.Clear();
        viewModel.Reset();
    }

    private void OnBackspaceClicked(object? sender, EventArgs e)
    {
        var query = getQuery();
        if (query.Length == 0) return;

        // Drop the last Unicode scalar, not the last UTF-16 unit: kanji outside
        // the BMP are surrogate pairs.
        var cut = query.Length >= 2 && char.IsLowSurrogate(query[^1]) && char.IsHighSurrogate(query[^2]) ? 2 : 1;
        setQuery(query[..^cut]);
        viewModel.ComposedQuery = getQuery();
    }

    // Portrait canvas height; in landscape the canvas is a square whose side fits
    // what's left of the page after the header, the sheet's bottom padding and the
    // system bars (the reported page height includes them, ~55dp on the test
    // phone, but the sheet can't draw under them). The Undo/Clear/Strokes buttons
    // sit in a column beside the canvas in landscape, so unlike the old 210 this
    // no longer includes their row (44dp button + 10dp margin = 54): 210 - 54 = 156.
    // The Append-mode query line adds its own height to the chrome.
    private const double PortraitCanvasHeight = 230;
    private const double LandscapeChromeHeight = 156;
    private const double LandscapeButtonColumnWidth = 104;
    private const double QueryLineChromeHeight = 56;
    private const double MinCanvasHeight = 120;

    private bool? isLandscape;
    private double pageHeight;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        pageHeight = height;
        var landscape = width > height;
        if (landscape != isLandscape)
        {
            isLandscape = landscape;
            Body.ColumnDefinitions = landscape
                ? new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star) }
                : new ColumnDefinitionCollection { new(GridLength.Star) };
            ArrangeCanvasButtons(landscape);
            MatchesRule.IsVisible = !landscape;
            Grid.SetColumn(MatchesSection, landscape ? 1 : 0);
            Grid.SetRow(MatchesSection, landscape ? 0 : 2);

            // The mode switch shares the header row in landscape to save a row.
            Grid.SetRow(ModeSwitch, landscape ? 0 : 1);
            Grid.SetColumn(ModeSwitch, landscape ? 1 : 0);
            Grid.SetColumnSpan(ModeSwitch, landscape ? 1 : 3);
            ModeSwitch.Margin = landscape ? new Thickness(0, 0, 10, 0) : new Thickness(0, 12, 0, 0);
            ModeSwitch.HorizontalOptions = landscape ? LayoutOptions.End : LayoutOptions.Fill;
            ModeSwitch.VerticalOptions = LayoutOptions.Center;
        }

        ApplyCanvasHeight();
    }

    // Portrait: Undo | Clear | Strokes in a row under the canvas. Landscape: the
    // same three stacked in a column to the right of the canvas.
    private void ArrangeCanvasButtons(bool landscape)
    {
        DrawSection.ColumnDefinitions = landscape
            ? new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) }
            : new ColumnDefinitionCollection { new(GridLength.Star) };
        DrawSection.RowDefinitions = landscape
            ? new RowDefinitionCollection { new(GridLength.Auto) }
            : new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) };

        Grid.SetRow(CanvasButtons, landscape ? 0 : 1);
        Grid.SetColumn(CanvasButtons, landscape ? 1 : 0);
        CanvasButtons.Margin = landscape ? new Thickness(8, 0, 0, 0) : new Thickness(0, 10, 0, 0);
        CanvasButtons.WidthRequest = landscape ? LandscapeButtonColumnWidth : -1;
        CanvasButtons.ColumnSpacing = landscape ? 0 : 8;
        CanvasButtons.RowSpacing = landscape ? 8 : 0;
        CanvasButtons.ColumnDefinitions = landscape
            ? new ColumnDefinitionCollection { new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) };
        CanvasButtons.RowDefinitions = landscape
            ? new RowDefinitionCollection { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }
            : new RowDefinitionCollection();

        View[] controls = [UndoButton, ClearButton, StrokesBox];
        for (var i = 0; i < controls.Length; i++)
        {
            Grid.SetRow(controls[i], landscape ? i : 0);
            Grid.SetColumn(controls[i], landscape ? 0 : i);
        }
    }

    private void ApplyCanvasHeight()
    {
        if (pageHeight <= 0) return;

        if (isLandscape == true)
        {
            var chrome = LandscapeChromeHeight + (viewModel.IsAppendMode ? QueryLineChromeHeight : 0);
            var side = Math.Clamp(pageHeight - chrome, MinCanvasHeight, PortraitCanvasHeight);
            CanvasHost.WidthRequest = side;
            CanvasHost.HeightRequest = side;
        }
        else
        {
            CanvasHost.WidthRequest = -1;
            CanvasHost.HeightRequest = PortraitCanvasHeight;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DrawPad.Clear();
        viewModel.Reset();
        viewModel.ComposedQuery = getQuery();
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
