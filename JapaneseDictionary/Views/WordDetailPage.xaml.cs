using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class WordDetailPage : ContentPage
{
    private readonly WordDetailViewModel viewModel;

    public WordDetailPage(WordDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        viewModel.OnDisappearing();
    }
}
