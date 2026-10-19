using JapaneseDictionary.Views;

namespace JapaneseDictionary;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(WordDetailPage), typeof(WordDetailPage));
        Routing.RegisterRoute(nameof(KanjiWordsPage), typeof(KanjiWordsPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ListsPage), typeof(ListsPage));
        Routing.RegisterRoute(nameof(ListDetailPage), typeof(ListDetailPage));
    }
}