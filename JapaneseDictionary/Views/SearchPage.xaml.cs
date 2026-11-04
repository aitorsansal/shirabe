using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel viewModel;

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    // Results already update while typing, so Enter only has to get the keyboard
    // out of the way: it covers all but two or three results on a phone.
    private async void OnQueryCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry entry) return;
        await entry.HideSoftInputAsync(CancellationToken.None);
        entry.Unfocus();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.OnAppearingAsync();
    }
}
