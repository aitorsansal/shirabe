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

    private bool? isLandscape;

    // Landscape is ~344dp tall, so the stacked header would eat half of it: put the
    // search bar between wordmark and icons instead. Only touched when the
    // orientation flips.
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        var landscape = width > height;
        if (landscape == isLandscape) return;
        isLandscape = landscape;

        Header.ColumnDefinitions = landscape
            ? new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) };

        Grid.SetRow(Wordmark, 0);
        Grid.SetColumn(Wordmark, 0);

        Grid.SetRow(HeaderIcons, 0);
        Grid.SetColumn(HeaderIcons, landscape ? 2 : 1);

        Grid.SetRow(SearchBar, landscape ? 0 : 2);
        Grid.SetColumn(SearchBar, landscape ? 1 : 0);
        Grid.SetColumnSpan(SearchBar, landscape ? 1 : 2);
        SearchBar.Margin = landscape ? new Thickness(12, 0) : new Thickness(0);
        SearchBar.VerticalOptions = landscape ? LayoutOptions.Center : LayoutOptions.Fill;

        Grid.SetRow(HeaderRule, 1);
        Grid.SetColumn(HeaderRule, 0);
        Grid.SetColumnSpan(HeaderRule, landscape ? 3 : 2);
        HeaderRule.Margin = landscape ? new Thickness(0, 10, 0, 10) : new Thickness(0, 12, 0, 14);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.OnAppearingAsync();
    }
}
