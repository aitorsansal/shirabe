using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

/// <summary>
/// Modal sheet for saving an entry to lists. Like HandwritingSheetPage it's
/// opened from code, not Shell-routed; the caller sets <see cref="Closed"/>
/// to refresh whatever depends on list membership (the word page's heart).
/// </summary>
public partial class AddToListSheetPage : ContentPage
{
    private readonly AddToListViewModel viewModel;

    public Action? Closed { get; set; }

    public AddToListSheetPage(AddToListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    public Task LoadAsync(int entryId, string word) => viewModel.LoadAsync(entryId, word);

    // Header, rules, "new list" footer, bottom padding, and a strip of backdrop
    // to tap to dismiss: what the list can't use of the page height.
    private const double SheetChromeHeight = 190;
    private const double MaxListHeight = 380;
    private const double MinListHeight = 108;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (height <= 0) return;
        ListScroll.MaximumHeightRequest = Math.Clamp(height - SheetChromeHeight, MinListHeight, MaxListHeight);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Closed?.Invoke();
    }

    private async void OnDoneClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
