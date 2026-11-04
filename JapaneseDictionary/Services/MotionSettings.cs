using Android.Provider;

namespace JapaneseDictionary.Services;

public static class MotionSettings
{
    /// <summary>False when the user turned Android animations off (the system
    /// animator duration scale is 0); autoplaying motion should then stay still.</summary>
    public static bool AnimationsEnabled
    {
        get
        {
            try
            {
                var resolver = Android.App.Application.Context.ContentResolver;
                return Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f) > 0f;
            }
            catch
            {
                return true;
            }
        }
    }
}
