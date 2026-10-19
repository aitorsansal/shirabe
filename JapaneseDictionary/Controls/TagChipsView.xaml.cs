using JapaneseDictionary.Services;
using JapaneseDictionary.ViewModels;

namespace JapaneseDictionary.Controls;

public partial class TagChipsView : ContentView
{
    public static readonly BindableProperty TagsProperty = BindableProperty.Create(
        nameof(Tags), typeof(IEnumerable<TagChip>), typeof(TagChipsView),
        propertyChanged: (view, _, tags) => BindableLayout.SetItemsSource(((TagChipsView)view).Chips, (IEnumerable<TagChip>?)tags));

    public IEnumerable<TagChip>? Tags
    {
        get => (IEnumerable<TagChip>?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    /// <summary>Explains the chip under <paramref name="position"/> (relative to this
    /// view), if any. Used by a parent whose own tap recognizer would otherwise win.</summary>
    public bool TryShowTagAt(Point? position)
    {
        if (position is not { } point) return false;

        // The chips are small; give a fingertip a little slack around each.
        foreach (var chip in Chips.Children.OfType<View>())
        {
            if (chip.BindingContext is TagChip tag && chip.Bounds.Inflate(4, 4).Contains(point))
            {
                TagInfo.ShowCommand.Execute(tag);
                return true;
            }
        }

        return false;
    }

    public TagChipsView()
    {
        InitializeComponent();
    }
}
