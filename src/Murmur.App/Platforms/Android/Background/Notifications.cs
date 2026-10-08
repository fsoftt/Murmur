using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace Murmur.App.Platforms.Android.Background;

/// <summary>Notification channels and builders. Notifications never contain message text.</summary>
internal static class Notifications
{
    public const string AvailabilityChannel = "availability";
    public const string MessagesChannel = "messages";
    public const int AvailabilityNotificationId = 1001;

    public static void EnsureChannels(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        manager.CreateNotificationChannel(new NotificationChannel(AvailabilityChannel, "Disponibilidad", NotificationImportance.Min)
        {
            Description = "Aviso permanente mientras Murmur está disponible para tus contactos.",
        });
        manager.CreateNotificationChannel(new NotificationChannel(MessagesChannel, "Mensajes", NotificationImportance.High)
        {
            Description = "Nuevos mensajes de tus contactos.",
        });
    }

    public static Notification Availability(Context context) =>
        new NotificationCompat.Builder(context, AvailabilityChannel)
            .SetSmallIcon(Resource.Drawable.ic_stat_murmur)!
            .SetContentTitle("Murmur está disponible")!
            .SetContentText("Tus contactos pueden enviarte mensajes aunque la app esté cerrada.")!
            .SetOngoing(true)!
            .SetSilent(true)!
            .SetPriority(NotificationCompat.PriorityMin)!
            .SetContentIntent(OpenApp(context))!
            .Build()!;

    public static Notification NewMessage(Context context, string sender) =>
        new NotificationCompat.Builder(context, MessagesChannel)
            .SetSmallIcon(Resource.Drawable.ic_stat_murmur)!
            .SetContentTitle(sender)!
            .SetContentText("Nuevo mensaje")!
            .SetAutoCancel(true)!
            .SetCategory(NotificationCompat.CategoryMessage)!
            .SetVisibility(NotificationCompat.VisibilityPrivate)!
            .SetPriority(NotificationCompat.PriorityHigh)!
            .SetContentIntent(OpenApp(context))!
            .Build()!;

    private static PendingIntent? OpenApp(Context context)
    {
        var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        return intent is null ? null : PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }
}
