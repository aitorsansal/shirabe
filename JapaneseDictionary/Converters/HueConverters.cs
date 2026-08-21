using System.Globalization;

namespace JapaneseDictionary.Converters;

/// <summary>
/// The five-hue accent palette (verde/lima/amarillo/azul/morado) cycled across
/// word-class tags, JLPT/id badges, and kanji meta chips — see Colors.xaml's
/// Hue0..Hue4 Fill/Text/Border tokens, plus the special-cased "common" badge
/// (fixed lime fill, ink text — see Colors.xaml's CommonBadgeFill). Three
/// binding shapes are supported by the same converters:
///  - a fixed hue known at XAML-authoring time (kanji meta chips): single-value
///    Binding to ThemeService.IsDark, with the hue index as ConverterParameter;
///  - a per-item hue from a bound collection with no "common" concept (search
///    tags): MultiBinding of [ItemHueIndex, ThemeService.IsDark];
///  - a per-item hue from a bound collection that can BE the common badge
///    (entry badges): MultiBinding of [IsCommon, ItemHueIndex, ThemeService.IsDark].
/// All three react live when the user toggles theme. Plain converters can't do
/// this alone: Label.TextColor is a Color, not a Brush, so it can't be
/// pre-packaged as a theme-reactive resource the way Border.Stroke/Background
/// can (see the AppThemeBinding usage everywhere else). Everything stays in
/// one flat, single-template FlexLayout per list — a badge can't be split into
/// "one static common child + one bound FlexLayout of the rest" because a
/// FlexLayout nested inside another FlexLayout doesn't report its own wrapped
/// height back to the outer one, silently clipping a second wrapped row.
/// </summary>
public abstract class HueConverterBase : IValueConverter, IMultiValueConverter
{
    private const int HueCount = 5;

    protected abstract string Role { get; } // "Fill", "Text", or "Border"

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hue = parameter is string s ? int.Parse(s, CultureInfo.InvariantCulture) : (int)(parameter ?? 0);
        var isDark = value is true;
        return Resolve(hue, isDark);
    }

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length >= 3)
        {
            var isCommon = values[0] is true;
            var hue = values[1] is int h ? h : 0;
            var darkFor3 = values[2] is true;
            return isCommon ? ResolveCommon(darkFor3) : Resolve(hue, darkFor3);
        }

        var hue2 = values.Length > 0 && values[0] is int i ? i : 0;
        var isDark2 = values.Length > 1 && values[1] is true;
        return Resolve(hue2, isDark2);
    }

    private Color Resolve(int hue, bool isDark)
    {
        var index = ((hue % HueCount) + HueCount) % HueCount;
        return Lookup($"Hue{index}{Role}{(isDark ? "Dark" : "Light")}");
    }

    private Color ResolveCommon(bool isDark) => Role switch
    {
        "Text" => Lookup(isDark ? "InkPrimaryDark" : "InkPrimaryLight"),
        _ => Lookup(isDark ? "CommonBadgeFillDark" : "CommonBadgeFillLight"), // Fill and Border both use the fill — a plain lime chip has no separate outline.
    };

    private static Color Lookup(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var color) && color is Color c ? c : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class HueFillConverter : HueConverterBase
{
    protected override string Role => "Fill";
}

public sealed class HueTextConverter : HueConverterBase
{
    protected override string Role => "Text";
}

public sealed class HueBorderConverter : HueConverterBase
{
    protected override string Role => "Border";
}
