namespace JapaneseDictionary.Controls;

/// <summary>
/// Freehand stroke-capture surface for drawing a kanji to search by. Records
/// ordered strokes as point lists; drawing/rasterizing/recognition all happen
/// outside this control — it only captures and renders ink.
/// </summary>
public sealed class KanjiDrawPad : GraphicsView
{
    private readonly List<List<PointF>> strokes = [];
    private List<PointF>? current;

    public static readonly BindableProperty InkColorProperty = BindableProperty.Create(
        nameof(InkColor), typeof(Color), typeof(KanjiDrawPad), Colors.Black,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness), typeof(double), typeof(KanjiDrawPad), 7.0,
        propertyChanged: OnVisualPropertyChanged);

    public KanjiDrawPad()
    {
        Drawable = new PadDrawable(this);

        StartInteraction += (_, e) =>
        {
            current = [e.Touches[0]];
            Invalidate();
        };
        DragInteraction += (_, e) =>
        {
            current?.Add(e.Touches[0]);
            Invalidate();
        };
        EndInteraction += (_, _) =>
        {
            // >1, not >3: a fast, short real stroke (e.g. a straight line in 一)
            // can legitimately produce only 2-3 DragInteraction samples on a
            // phone touchscreen. A stricter threshold silently drops valid strokes.
            if (current is { Count: > 1 })
            {
                strokes.Add(current);
                StrokeCompleted?.Invoke(this, strokes.Count);
            }

            current = null;
            Invalidate();
        };
    }

    public event EventHandler<int>? StrokeCompleted;

    public Color InkColor
    {
        get => (Color)GetValue(InkColorProperty);
        set => SetValue(InkColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IReadOnlyList<IReadOnlyList<PointF>> Strokes => strokes;
    public bool HasStrokes => strokes.Count > 0;

    public void Clear()
    {
        strokes.Clear();
        current = null;
        Invalidate();
    }

    /// <summary>Removes the most recently completed stroke, if any.</summary>
    public void Undo()
    {
        if (strokes.Count == 0) return;
        strokes.RemoveAt(strokes.Count - 1);
        Invalidate();
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is KanjiDrawPad pad) pad.Invalidate();
    }

    private sealed class PadDrawable(KanjiDrawPad owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = owner.InkColor;
            canvas.StrokeSize = (float)owner.StrokeThickness;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            foreach (var stroke in owner.strokes)
                DrawStroke(canvas, stroke);

            if (owner.current is { Count: > 1 } c)
                DrawStroke(canvas, c);
        }

        private static void DrawStroke(ICanvas canvas, IReadOnlyList<PointF> points)
        {
            if (points.Count < 2) return;

            var path = new PathF();
            path.MoveTo(points[0]);
            for (var i = 1; i < points.Count; i++)
                path.LineTo(points[i]);
            canvas.DrawPath(path);
        }
    }
}
