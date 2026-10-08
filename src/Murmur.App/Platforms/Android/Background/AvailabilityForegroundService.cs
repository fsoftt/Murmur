using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Murmur.App.Services;

namespace Murmur.App.Platforms.Android.Background;

/// <summary>
/// Option B, "always available": a foreground service (with its required permanent notification)
/// that keeps the client connected to the signaling service while the app is closed, so contacts
/// can reach this phone. Nothing is stored on any server; the phone itself stays reachable.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeRemoteMessaging)]
public sealed class AvailabilityForegroundService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Notifications.EnsureChannels(this);
        var notification = Notifications.Availability(this);
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            StartForeground(Notifications.AvailabilityNotificationId, notification, ForegroundService.TypeRemoteMessaging);
        }
        else
        {
            StartForeground(Notifications.AvailabilityNotificationId, notification);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (IPlatformApplication.Current?.Services.GetService(typeof(ClientHost)) is ClientHost host)
                {
                    await host.InitializeAsync();
                }
            }
            catch (Exception)
            {
                // The UI reports storage problems when opened; the service simply stays idle.
            }
        });

        // Restarted by the system with a null intent if the process is killed.
        return StartCommandResult.Sticky;
    }

    /// <summary>
    /// Starts the service. Android 12+ only allows this while the app is visible or from exempt
    /// moments such as boot; elsewhere the OS refuses and the mode resumes on the next app start.
    /// </summary>
    public static bool Start(Context context)
    {
        try
        {
            var intent = new Intent(context, typeof(AvailabilityForegroundService));
            AndroidX.Core.Content.ContextCompat.StartForegroundService(context, intent);
            return true;
        }
        catch (Java.Lang.IllegalStateException)
        {
            // ForegroundServiceStartNotAllowedException derives from IllegalStateException.
            return false;
        }
    }

    public static void Stop(Context context) =>
        context.StopService(new Intent(context, typeof(AvailabilityForegroundService)));
}
