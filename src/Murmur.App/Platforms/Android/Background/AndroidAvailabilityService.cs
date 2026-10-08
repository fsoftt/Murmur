using Android.Content;
using Android.OS;
using Murmur.Presentation.Services;

namespace Murmur.App.Platforms.Android.Background;

public sealed class AndroidAvailabilityService : IAvailabilityService
{
    private const string PreferenceKey = "availability.always";

    internal static bool IsEnabledPreference => Preferences.Default.Get(PreferenceKey, false);

    public bool IsSupported => true;

    public bool IsAlwaysAvailable => IsEnabledPreference;

    public bool IsBatteryOptimized
    {
        get
        {
            var context = Platform.AppContext;
            return context.GetSystemService(Context.PowerService) is PowerManager power
                && !power.IsIgnoringBatteryOptimizations(context.PackageName);
        }
    }

    public async Task<bool> SetAlwaysAvailableAsync(bool enabled)
    {
        var context = Platform.AppContext;
        if (enabled)
        {
            // Android 13+ hides the required notification (and the service) without this permission.
            if (OperatingSystem.IsAndroidVersionAtLeast(33)
                && await Permissions.RequestAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
            {
                Preferences.Default.Set(PreferenceKey, false);
                return false;
            }

            Preferences.Default.Set(PreferenceKey, true);
            return AvailabilityForegroundService.Start(context);
        }

        Preferences.Default.Set(PreferenceKey, false);
        AvailabilityForegroundService.Stop(context);
        return false;
    }

    public void ScheduleBackgroundDelivery()
    {
        try
        {
            BackgroundDelivery.Schedule(Platform.AppContext);
        }
        catch (Java.Lang.Exception)
        {
            // WorkManager unavailable (e.g. during process teardown): the next app start reschedules.
        }
    }

    public void ResumeIfEnabled()
    {
        if (IsEnabledPreference)
        {
            AvailabilityForegroundService.Start(Platform.AppContext);
        }
    }

    public void OpenBatterySettings()
    {
        var intent = new Intent(global::Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        Platform.AppContext.StartActivity(intent);
    }
}
