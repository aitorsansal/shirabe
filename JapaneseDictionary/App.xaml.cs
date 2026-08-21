using Microsoft.Extensions.DependencyInjection;

using JapaneseDictionary.Services;

namespace JapaneseDictionary;

public partial class App : Application
{
    /// <summary>Service-locator escape hatch for resolving transient pages
    /// from ViewModels (e.g. the handwriting sheet), which aren't themselves
    /// DI-constructed the way Shell-routed pages are.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        Services = services;

        // Registered as a keyed resource (not just DI) so pages can bind to it
        // directly in XAML — e.g. the theme toggle button and the hue-chip
        // MultiBindings in HueConverters.cs, which need to react live when the
        // user switches theme.
        Resources["ThemeService"] = services.GetRequiredService<ThemeService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}