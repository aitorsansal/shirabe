using Android.Widget;

using JapaneseDictionary.Resources.Localization;

namespace JapaneseDictionary.Services;

public static class ClipboardCopy
{
    public static async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;

        await Clipboard.Default.SetTextAsync(text);

        // Android 13+ shows its own "copied" confirmation; earlier versions say nothing.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            Toast.MakeText(Platform.AppContext, AppResources.Common_Copied, ToastLength.Short)?.Show();
    }
}
