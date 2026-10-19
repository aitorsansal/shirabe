using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class KanjiWordsPage : ContentPage
{
    private readonly KanjiWordsViewModel viewModel;

    public KanjiWordsPage(KanjiWordsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.OnAppearingAsync();
    }
}
