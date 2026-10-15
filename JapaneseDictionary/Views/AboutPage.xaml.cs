using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage(AboutViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // The link rows sit two BindableLayouts deep, so their own BindingContext is an
    // AboutLink rather than the page's view model; routing through a tap handler avoids
    // a RelativeSource binding back to the view model.
    private void OnLinkTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string url && BindingContext is AboutViewModel viewModel)
            viewModel.OpenLinkCommand.Execute(url);
    }
}
