using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel viewModel;

    public SettingsPage(SettingsViewModel viewModel)
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
