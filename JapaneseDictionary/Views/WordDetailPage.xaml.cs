using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class WordDetailPage : ContentPage
{
    public WordDetailPage(WordDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
