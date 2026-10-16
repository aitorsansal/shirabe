using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class ListsPage : ContentPage
{
    private readonly ListsViewModel viewModel;

    public ListsPage(ListsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    // Counts change while away (a word page saved or removed something).
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.OnAppearingAsync();
    }
}
