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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Closed?.Invoke();
    }

    private async void OnDoneClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
