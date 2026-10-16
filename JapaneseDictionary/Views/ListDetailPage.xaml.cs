using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class ListDetailPage : ContentPage
{
    private readonly ListDetailViewModel viewModel;

    public ListDetailPage(ListDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    // Reload on every appearance: an opened word may have been removed from
    // this list (or un-favourited) before coming back.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.OnAppearingAsync();
    }
}
