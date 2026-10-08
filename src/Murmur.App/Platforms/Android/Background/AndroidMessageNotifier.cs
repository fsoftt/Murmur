using Android.App;
using Android.Content;
using Murmur.App.Services;
using Murmur.Client;
using Murmur.Domain.Model;

namespace Murmur.App.Platforms.Android.Background;

/// <summary>
/// Posts "New message from X" when a message arrives while no Murmur screen is visible. The text
/// is never shown in the notification, so it cannot leak onto the lock screen.
/// </summary>
public sealed class AndroidMessageNotifier : IIncomingMessageNotifier
{
    private int _attached;

    public void Attach(MurmurClient client)
    {
        if (Interlocked.Exchange(ref _attached, 1) == 1)
        {
            return;
        }

        client.Events.MessageStored += (_, message) =>
        {
            if (message.Direction == MessageDirection.Incoming && !AppVisibility.IsVisible)
            {
                _ = NotifyAsync(client, message);
            }
        };
    }

    private static async Task NotifyAsync(MurmurClient client, Message message)
    {
        try
        {
            var contact = await client.FindContactForConversationAsync(message.ConversationId);
            var context = Platform.AppContext;
            Notifications.EnsureChannels(context);
            if (context.GetSystemService(Context.NotificationService) is NotificationManager manager)
            {
                // One notification per conversation, replaced by newer messages.
                manager.Notify(message.ConversationId.GetHashCode(), Notifications.NewMessage(context, contact?.DisplayName ?? "Murmur"));
            }
        }
        catch (Exception)
        {
            // A missing notification must never affect message storage.
        }
    }
}
