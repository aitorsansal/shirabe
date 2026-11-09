using Android.Content;
using Android.Content.PM;

namespace JapaneseDictionary;

public enum PracticeAppStatus
{
    Absent,
    TooOld,
    Ok,
}

public static class PracticeApp
{
    private const string Package = "com.aitorsansal.nihongonopractise";
    private const long NnpMinVersionCode = 24;

    public static PracticeAppStatus GetStatus()
    {
        try
        {
            using var info = Android.App.Application.Context.PackageManager!.GetPackageInfo(Package, (PackageInfoFlags)0);
            var version = OperatingSystem.IsAndroidVersionAtLeast(28) ? info!.LongVersionCode : info!.VersionCode;
            return version >= NnpMinVersionCode ? PracticeAppStatus.Ok : PracticeAppStatus.TooOld;
        }
        catch (PackageManager.NameNotFoundException)
        {
            return PracticeAppStatus.Absent;
        }
    }

    public static async Task<PracticeAppStatus> SendAsync(byte[] json, Guid requestId)
    {
        var directory = Path.Combine(FileSystem.CacheDirectory, "companion");
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{requestId:D}.json");
        await File.WriteAllBytesAsync(path, json);

        var context = Android.App.Application.Context;
        using var file = new Java.IO.File(path);
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.companion.files", file);
        using var intent = new Intent("com.aitorsansal.nihongonopractise.action.IMPORT_WORDS");
        intent.SetPackage(Package);
        intent.SetDataAndType(uri, "application/json");
        intent.PutExtra(CompanionIntents.VersionExtra, 1);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        try
        {
            context.StartActivity(intent);
            return PracticeAppStatus.Ok;
        }
        catch (ActivityNotFoundException)
        {
            return PracticeAppStatus.TooOld;
        }
    }

    public static bool TryOpen()
    {
        var context = Android.App.Application.Context;
        using var intent = context.PackageManager!.GetLaunchIntentForPackage(Package);
        if (intent == null) return false;
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        try
        {
            context.StartActivity(intent);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            return false;
        }
    }

    public static async Task OpenStoreAsync()
    {
        try
        {
            if (!await Launcher.TryOpenAsync($"market://details?id={Package}"))
                await Launcher.OpenAsync($"https://play.google.com/store/apps/details?id={Package}");
        }
        catch (Exception)
        {
            // No store and no browser; nothing useful to show.
        }
    }
}
