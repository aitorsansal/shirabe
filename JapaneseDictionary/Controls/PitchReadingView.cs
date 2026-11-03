using JapaneseDictionary.Core.Pitch;

using Font = Microsoft.Maui.Graphics.Font;

namespace JapaneseDictionary.Controls;

/// <summary>
/// A kana reading with its pitch accent drawn over it in the usual overline style:
/// a line over the high morae, lower over the low ones, stepping down at the
/// downstep, and a small circle after the word where a following particle would
/// sit — high after a flat word (heiban), low after a final-accented one (odaka).
/// With several accents each gets a row; the first is drawn in
/// <see cref="PrimaryColor"/>, the rest smaller and fainter. The control fills the
/// width it is given and works out its own height, so it lays out like a Label.
/// </summary>
public sealed class PitchReadingView : GraphicsView
{
    public static readonly BindableProperty ReadingProperty = BindableProperty.Create(
        nameof(Reading), typeof(string), typeof(PitchReadingView), "", propertyChanged: OnContentChanged);

    public static readonly BindableProperty AccentsProperty = BindableProperty.Create(
        nameof(Accents), typeof(IReadOnlyList<int>), typeof(PitchReadingView), null, propertyChanged: OnContentChanged);

    public static readonly BindableProperty PrimaryColorProperty = BindableProperty.Create(
        nameof(PrimaryColor), typeof(Color), typeof(PitchReadingView), Colors.Black, propertyChanged: OnContentChanged);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(PitchReadingView), 14.0, propertyChanged: OnContentChanged);

    // Same face as the plain reading label (see WordDetailPage.xaml).
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(PitchReadingView), "ArchivoRegular", propertyChanged: OnContentChanged);

    public string Reading
    {
        get => (string)GetValue(ReadingProperty);
        set => SetValue(ReadingProperty, value);
    }

    public IReadOnlyList<int>? Accents
    {
        get => (IReadOnlyList<int>?)GetValue(AccentsProperty);
        set => SetValue(AccentsProperty, value);
    }

    public Color PrimaryColor
    {
        get => (Color)GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public PitchReadingView()
    {
        Drawable = new PitchDrawable(this);
    }

    private const double SecondaryScale = 0.8;
    private const float SecondaryAlpha = 0.65f;

    private static void OnContentChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (PitchReadingView)bindable;
        view.HeightRequest = view.ComputeHeight();
        view.Invalidate();
    }

    // The accents that fit the word; a Kanjium accent beyond the mora count is bad data.
    private List<int> ValidAccents()
    {
        var count = Morae.Count(Reading);
        return (Accents ?? []).Where(a => a >= 0 && a <= count).ToList();
    }

    private static double LineHeight(double fontSize) => Math.Ceiling(fontSize * 0.55) + Math.Ceiling(fontSize * 1.25);

    private double ComputeHeight()
    {
        var accents = ValidAccents();
        if (accents.Count == 0) return LineHeight(FontSize);

        var height = LineHeight(FontSize);
        for (var i = 1; i < accents.Count; i++) height += LineHeight(FontSize * SecondaryScale) + 2;
        return height;
    }

    private sealed class PitchDrawable(PitchReadingView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var accents = owner.ValidAccents();
            var morae = Morae.Split(owner.Reading);
            if (morae.Count == 0) return;

            var y = 0f;
            for (var i = 0; i < accents.Count; i++)
            {
                var primary = i == 0;
                var size = (float)(owner.FontSize * (primary ? 1 : SecondaryScale));
                var color = primary ? owner.PrimaryColor : owner.PrimaryColor.WithAlpha(SecondaryAlpha);
                DrawLine(canvas, morae, accents[i], y, size, color);
                y += (float)LineHeight(size) + 2;
            }
        }

        private void DrawLine(ICanvas canvas, IReadOnlyList<string> morae, int accent, float top, float size, Color color)
        {
            var font = new Font(owner.FontFamily);
            canvas.Font = font;
            canvas.FontSize = size;
            canvas.FontColor = color;

            var bandHeight = (float)Math.Ceiling(size * 0.55);
            var textHeight = (float)Math.Ceiling(size * 1.25);
            var highY = top + 1.5f;
            var lowY = top + bandHeight - 1.5f;

            // Mora boundaries: edges[i] is the left edge of mora i, edges[^1] the right edge of the word.
            var edges = new float[morae.Count + 1];
            for (var i = 0; i < morae.Count; i++)
            {
                var width = canvas.GetStringSize(morae[i], font, size).Width;
                canvas.DrawString(morae[i], edges[i], top + bandHeight, width, textHeight,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                edges[i + 1] = edges[i] + width;
            }

            var pattern = PitchPattern.Compute(morae.Count, accent);
            float LevelY(bool high) => high ? highY : lowY;

            canvas.StrokeColor = color;
            canvas.StrokeSize = Math.Max(1.2f, size / 10f);
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            var path = new PathF();
            path.MoveTo(edges[0], LevelY(pattern[0]));
            for (var i = 0; i < morae.Count; i++)
            {
                path.LineTo(edges[i + 1], LevelY(pattern[i]));
                // Step to the next mora's level (the last step is to the particle's).
                if (pattern[i + 1] != pattern[i]) path.LineTo(edges[i + 1], LevelY(pattern[i + 1]));
            }

            // Particle: a ring a little past the word, at its own level.
            var radius = Math.Max(2f, size / 6f);
            var gap = radius * 2.2f;
            var particleX = edges[^1] + gap + radius;
            var particleY = LevelY(pattern[^1]);
            path.LineTo(particleX - radius, particleY);
            canvas.DrawPath(path);
            canvas.DrawCircle(particleX, particleY, radius);
        }
    }
}
