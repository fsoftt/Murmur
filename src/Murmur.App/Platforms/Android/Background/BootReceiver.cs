using Android.App;
using Android.Content;

namespace Murmur.App.Platforms.Android.Background;

/// <summary>Restarts "always available" after the phone reboots, if the user enabled it.</summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced])]
public sealed class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && AndroidAvailabilityService.IsEnabledPreference)
        {
            AvailabilityForegroundService.Start(context);
        }
    }
}
