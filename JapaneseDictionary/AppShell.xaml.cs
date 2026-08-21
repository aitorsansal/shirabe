using JapaneseDictionary.Views;

namespace JapaneseDictionary;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(WordDetailPage), typeof(WordDetailPage));
    }
}