using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using JapaneseDictionary.Resources.Localization;
using JapaneseDictionary.Services;
using JapaneseDictionary.Views;

namespace JapaneseDictionary.ViewModels;

/// <summary>One choice in the TTS engine picker; empty <see cref="Package"/>
/// means automatic.</summary>
public sealed partial class EngineChoiceRow : ObservableObject
{
    public required string Package { get; init; }
    public required string Label { get; init; }
    public string? Note { get; init; }

    [ObservableProperty]
    private bool isSelected;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private const string PracticeAppPackage = "com.aitorsansal.nihongonopractise";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PracticePromotionChevron))]
    private bool isPracticePromotionExpanded;

    public string PracticePromotionChevron => IsPracticePromotionExpanded ? "⌃" : "⌄";

    [RelayCommand]
    private void TogglePracticePromotion() => IsPracticePromotionExpanded = !IsPracticePromotionExpanded;

    private readonly ThemeService theme;
    private readonly JapaneseTtsService tts;
    private readonly AppDataContext data;
    private readonly LanguageService language;

    public SettingsViewModel(ThemeService theme, JapaneseTtsService tts, AppDataContext data, LanguageService language)
    {
        this.theme = theme;
        this.tts = tts;
        this.data = data;
        this.language = language;
    }

    public bool IsThemeSystem => theme.Preference == ThemePreference.System;
    public bool IsThemeLight => theme.Preference == ThemePreference.Light;
    public bool IsThemeDark => theme.Preference == ThemePreference.Dark;

    public bool IsLanguageSystem => LanguageService.Preference == "";
    public bool IsLanguageEnglish => LanguageService.Preference == "en";
    public bool IsLanguageSpanish => LanguageService.Preference == "es";
    public bool IsLanguageCatalan => LanguageService.Preference == "ca";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoiceStatusText))]
    [NotifyPropertyChangedFor(nameof(IsVoiceReady))]
    private bool? isVoiceAvailable;

    public bool IsVoiceReady => IsVoiceAvailable == true;

    public string VoiceStatusText => IsVoiceAvailable switch
    {
        true => AppResources.Settings_VoiceReady,
        false => AppResources.Settings_VoiceMissing,
        null => AppResources.Settings_VoiceChecking,
    };

    /// <summary>Only shown when more than one engine can speak Japanese;
    /// with one (or none) there is nothing to choose.</summary>
    public ObservableCollection<EngineChoiceRow> EngineChoices { get; } = [];

    [ObservableProperty]
    private bool showEngineChoice;

    [ObservableProperty]
    private string historySummary = "";

    [ObservableProperty]
    private bool hasHistory;

    public string VersionText => UiText.Format(AppResources.Settings_Version, AppInfo.Current.VersionString, AppInfo.Current.BuildString);

    public async Task OnAppearingAsync()
    {
        // Re-check every time: the user may be coming back from installing a voice.
        tts.Invalidate();
        IsVoiceAvailable = await tts.IsAvailableAsync();
        await RefreshEngineChoicesAsync();

        await data.EnsureInitializedAsync();
        RefreshHistory();
    }

    [RelayCommand]
    private async Task SelectEngineAsync(EngineChoiceRow row)
    {
        if (row.IsSelected) return;
        tts.PreferredEngine = row.Package;
        foreach (var choice in EngineChoices) choice.IsSelected = choice == row;

        IsVoiceAvailable = await tts.IsAvailableAsync();
        await TestVoiceAsync();
    }

    private async Task RefreshEngineChoicesAsync()
    {
        var engines = await tts.GetJapaneseEnginesAsync();
        var preferred = tts.PreferredEngine;
        if (preferred.Length > 0 && engines.All(e => e.Package != preferred))
            preferred = ""; // pinned engine was uninstalled or lost its Japanese voice

        EngineChoices.Clear();
        EngineChoices.Add(new EngineChoiceRow
        {
            Package = "",
            Label = AppResources.Settings_EngineAutomatic,
            Note = AppResources.Settings_EngineAutomaticNote,
            IsSelected = preferred.Length == 0,
        });
        foreach (var engine in engines)
            EngineChoices.Add(new EngineChoiceRow { Package = engine.Package, Label = engine.Label, IsSelected = engine.Package == preferred });

        ShowEngineChoice = engines.Count > 1;
    }

    [RelayCommand]
    private void SetTheme(string value)
    {
        if (!Enum.TryParse<ThemePreference>(value, out var preference)) return;

        // This page is the only place the theme changes, so it notifies itself
        // rather than subscribing to the singleton ThemeService (which would
        // keep every transient instance of this view model alive).
        theme.SetPreference(preference);
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
    }

    /// <summary>Strings are read when a page is built, so the whole Shell is
    /// rebuilt in the new language and Settings reopened on top of it, leaving
    /// the user where they were.</summary>
    [RelayCommand]
    private async Task SetLanguageAsync(string value)
    {
        if (value == LanguageService.Preference) return;
        language.SetPreference(value);

        var window = Application.Current!.Windows[0];
        window.Page = new AppShell();
        await Shell.Current.GoToAsync(nameof(SettingsPage), animate: false);
    }

    [RelayCommand]
    private Task TestVoiceAsync() => tts.SpeakAsync("こんにちは。よろしくお願いします。");

    [RelayCommand]
    private static void OpenTtsSettings()
    {
#if ANDROID
        try
        {
            var intent = new Android.Content.Intent("com.android.settings.TTS_SETTINGS");
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Platform.CurrentActivity?.StartActivity(intent);
        }
        catch (Android.Content.ActivityNotFoundException)
        {
            // Some OEM builds don't expose the TTS screen directly; fall back to Settings.
            AppInfo.Current.ShowSettingsUI();
        }
#endif
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            AppResources.Settings_ClearHistoryTitle, AppResources.Settings_ClearHistoryMessage, AppResources.Settings_ClearHistoryConfirm, AppResources.Common_Cancel);
        if (!confirmed) return;

        data.UserDataRepository.ClearHistory();
        RefreshHistory();
    }

    [RelayCommand]
    private static async Task OpenPracticeAppAsync()
    {
        try
        {
            // Opens the Play Store app directly; the https URL is the fallback
            // when no store app handles market:// links.
            if (!await Launcher.TryOpenAsync($"market://details?id={PracticeAppPackage}"))
                await Launcher.OpenAsync($"https://play.google.com/store/apps/details?id={PracticeAppPackage}");
        }
        catch (Exception)
        {
            // No store and no browser; nothing useful to show.
        }
    }

    [RelayCommand]
    private static Task OpenAboutAsync() => Shell.Current.GoToAsync(nameof(AboutPage));

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private void RefreshHistory()
    {
        var count = data.UserDataRepository.HistoryCount;
        HasHistory = count > 0;
        HistorySummary = count switch
        {
            0 => AppResources.Settings_HistoryNone,
            1 => AppResources.Settings_HistoryOne,
            _ => UiText.Format(AppResources.Settings_HistoryMany, count),
        };
    }
}
