namespace JapaneseDictionary.Controls;

/// <summary>
/// Opt-in <c>controls:KeyboardOptions.NoFullscreen="True"</c> for an Entry: in landscape
/// Android otherwise shows the IME full-screen (extract mode), hiding the live results.
/// </summary>
public static class KeyboardOptions
{
    public static readonly BindableProperty NoFullscreenProperty = BindableProperty.CreateAttached(
        "NoFullscreen", typeof(bool), typeof(KeyboardOptions), false, propertyChanged: OnNoFullscreenChanged);

    public static bool GetNoFullscreen(BindableObject view) => (bool)view.GetValue(NoFullscreenProperty);
    public static void SetNoFullscreen(BindableObject view, bool value) => view.SetValue(NoFullscreenProperty, value);

    private static void OnNoFullscreenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not Entry entry) return;

        entry.Focused -= OnFocused;
        if ((bool)newValue) entry.Focused += OnFocused;
    }

    // MAUI rewrites EditText.ImeOptions after the handler connects (setting the
    // flags on connect or in the ReturnType mapping didn't survive, verified on
    // device). Focused is raised from the native focus change, before Android
    // starts the input connection and reads ImeOptions, so apply them here.
    private static void OnFocused(object? sender, FocusEventArgs e)
    {
#if ANDROID
        if (sender is not Entry entry || entry.Handler?.PlatformView is not Android.Widget.EditText editText) return;

        // ImeOptions is typed ImeAction but carries the ImeFlags bits too.
        editText.ImeOptions = (Android.Views.InputMethods.ImeAction)
            ((int)editText.ImeOptions
             | (int)Android.Views.InputMethods.ImeFlags.NoExtractUi
             | (int)Android.Views.InputMethods.ImeFlags.NoFullscreen);
#endif
    }
}
