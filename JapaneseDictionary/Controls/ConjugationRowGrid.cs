namespace JapaneseDictionary.Controls;

/// <summary>
/// Layout of one Conjugation-tab row. Exactly four children, in declaration order:
/// plain label, plain cell, polite label, polite cell. Narrow (default): two columns
/// and two rows, plain above polite. Once the row itself is at least
/// <see cref="WideThreshold"/> dp wide: one row of four columns. Width-based, not
/// orientation-based, so tablets and large font scales behave too.
/// </summary>
public class ConjugationRowGrid : Grid
{
    private const double WideThreshold = 560;

    private bool wide;

    public ConjugationRowGrid()
    {
        ColumnSpacing = 12;
        RowSpacing = 6;
        Arrange(false);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;

        // Only on a mode flip: re-laying out every allocation would loop.
        var shouldBeWide = width >= WideThreshold;
        if (shouldBeWide == wide) return;
        Arrange(shouldBeWide);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);
        PlaceChildren();
    }

    private void Arrange(bool isWide)
    {
        wide = isWide;
        ColumnDefinitions = isWide
            ? new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star) };
        RowDefinitions = isWide
            ? new RowDefinitionCollection { new(GridLength.Auto) }
            : new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) };
        PlaceChildren();
    }

    private void PlaceChildren()
    {
        if (Children.Count < 4) return;

        for (var i = 0; i < 4; i++)
        {
            var view = (BindableObject)Children[i];
            SetRow(view, wide ? 0 : i / 2);
            SetColumn(view, wide ? i : i % 2);
        }
    }
}
