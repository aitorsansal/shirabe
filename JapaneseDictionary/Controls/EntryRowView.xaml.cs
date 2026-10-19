using System.Windows.Input;

using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Controls;

/// <summary>
/// One dictionary entry row (search results, list contents). Tapping runs
/// <see cref="Command"/> with the entry id.
/// </summary>
public partial class EntryRowView : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EntryRowView));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public EntryRowView()
    {
        InitializeComponent();

        // A background (even transparent) so the whole row hit-tests, not just
        // its text, and so a SwipeView around it has an opaque surface to slide.
        this.SetAppThemeColor(BackgroundColorProperty,
            (Color)Application.Current!.Resources["ScreenBackgroundLight"],
            (Color)Application.Current!.Resources["ScreenBackgroundDark"]);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, e) =>
        {
            // The row's recognizer swallows taps meant for the chips inside it,
            // so route them by position.
            if (TagChips.TryShowTagAt(e.GetPosition(TagChips))) return;

            if (BindingContext is SearchResultRow row && Command?.CanExecute(row.EntryId) == true)
                Command.Execute(row.EntryId);
        };
        GestureRecognizers.Add(tap);
    }
}
