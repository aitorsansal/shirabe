using Microsoft.Extensions.Logging;

using JapaneseDictionary.Services;
using JapaneseDictionary.ViewModels;
using JapaneseDictionary.Views;

namespace JapaneseDictionary;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Archivo-Regular.ttf", "ArchivoRegular");
                fonts.AddFont("Archivo-Medium.ttf", "ArchivoMedium");
                fonts.AddFont("Archivo-SemiBold.ttf", "ArchivoSemiBold");
                fonts.AddFont("Archivo-Bold.ttf", "ArchivoBold");
                fonts.AddFont("Archivo-ExtraBold.ttf", "ArchivoExtraBold");
                fonts.AddFont("NotoSerifJP-Regular.ttf", "NotoSerifJPRegular");
                fonts.AddFont("NotoSerifJP-SemiBold.ttf", "NotoSerifJPSemiBold");
                // Subset to just 辞書 (2KB instead of 7.5MB): it's only used for the wordmark.
                fonts.AddFont("NotoSerifJP-Bold-Wordmark.ttf", "NotoSerifJPBoldWordmark");
                // Material Symbols Sharp, subset to the glyphs in Services/IconGlyphs.cs.
                fonts.AddFont("MaterialSymbolsSharp-Icons.ttf", "Icons");
                fonts.AddFont("MaterialSymbolsSharp-Filled.ttf", "IconsFilled");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Android's TextView reserves extra top/bottom space based on the font's
        // metrics tables (includeFontPadding, on by default) — Noto Serif JP's
        // tables are inflated enough that this shows up as a large, seemingly
        // unexplained gap above/below every Label using it. Disabling it applies
        // globally rather than needing per-Label position hacks.
        Microsoft.Maui.Handlers.LabelHandler.Mapper.AppendToMapping("NoFontPadding", (handler, _) =>
            handler.PlatformView.SetIncludeFontPadding(false));

        builder.Services.AddSingleton<AppDataContext>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<JapaneseTtsService>();

        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<WordDetailViewModel>();
        builder.Services.AddTransient<HandwritingViewModel>();
        builder.Services.AddTransient<AboutViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AddToListViewModel>();
        builder.Services.AddTransient<ListsViewModel>();
        builder.Services.AddTransient<ListDetailViewModel>();

        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<WordDetailPage>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ListsPage>();
        builder.Services.AddTransient<ListDetailPage>();
        builder.Services.AddTransient<HandwritingSheetPage>();
        builder.Services.AddTransient<AddToListSheetPage>();

        return builder.Build();
    }
}