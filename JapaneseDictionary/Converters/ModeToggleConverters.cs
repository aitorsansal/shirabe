using System.Globalization;

using Microsoft.Maui.Controls.Shapes;

namespace JapaneseDictionary.Converters;

/// <summary>Shared base for the two-segment mode toggle: the active side gets
/// a solid ink fill/background text, the inactive side stays transparent with
/// secondary-color text (see the mode toggle in the design handoff). Themed
/// via MultiBinding of [IsJapaneseMode, ThemeService.IsDark] since the ink/
/// background swap direction flips with the theme.</summary>
public abstract class ModeToggleConverterBase : IMultiValueConverter
{
    protected abstract bool ActiveWhenJapanese { get; }

    public abstract object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture);

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    protected bool IsActive(object?[] values) =>
        values.Length > 0 && values[0] is bool isJapanese && isJapanese == ActiveWhenJapanese;

    protected static bool IsDark(object?[] values) => values.Length > 1 && values[1] is true;
}

public sealed class JapaneseModeToBgConverter : ModeToggleConverterBase
{
    protected override bool ActiveWhenJapanese => true;

    public override object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        IsActive(values) ? Color.FromArgb(IsDark(values) ? "#f0eeec" : "#201e1d") : Colors.Transparent;
}

public sealed class JapaneseModeToFgConverter : ModeToggleConverterBase
{
    protected override bool ActiveWhenJapanese => true;

    public override object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        IsActive(values)
            ? Color.FromArgb(IsDark(values) ? "#17191c" : "#f3f2f2")
            : Color.FromArgb(IsDark(values) ? "#b7b3b1" : "#605d5d");
}

public sealed class EnglishModeToBgConverter : ModeToggleConverterBase
{
    protected override bool ActiveWhenJapanese => false;

    public override object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        IsActive(values) ? Color.FromArgb(IsDark(values) ? "#f0eeec" : "#201e1d") : Colors.Transparent;
}

public sealed class EnglishModeToFgConverter : ModeToggleConverterBase
{
    protected override bool ActiveWhenJapanese => false;

    public override object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        IsActive(values)
            ? Color.FromArgb(IsDark(values) ? "#17191c" : "#f3f2f2")
            : Color.FromArgb(IsDark(values) ? "#b7b3b1" : "#605d5d");
}

/// <summary>Active tab gets the fixed sky-blue primary-action fill (same in
/// both themes) with white text; inactive tabs stay transparent with
/// secondary-color text, themed via MultiBinding of [IsXTab, ThemeService.IsDark]
/// (see the sticky tab bar in the design handoff's Word Detail screen).</summary>
public sealed class TabActiveBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Color.FromArgb("#2f8fc4") : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TabActiveFgConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = values.Length > 0 && values[0] is true;
        if (isActive) return Colors.White;

        var isDark = values.Length > 1 && values[1] is true;
        return Color.FromArgb(isDark ? "#b7b3b1" : "#605d5d");
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts an SVG/path-markup "d" string (e.g. from KanjiVG stroke data)
/// into a <see cref="Geometry"/> for <c>Shapes.Path.Data</c>. Needed because
/// XAML's string-&gt;Geometry TypeConverter only fires for literal attribute
/// values at parse time — a data-bound string assigned to Data at runtime is
/// never converted, so without this the path silently never renders.
/// </summary>
public sealed class SvgPathToGeometryConverter : IValueConverter
{
    private static readonly PathGeometryConverter GeometryConverter = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } path) return null;
        return GeometryConverter.ConvertFromInvariantString(path);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound string is non-null/non-empty — used for IsVisible bindings.</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
