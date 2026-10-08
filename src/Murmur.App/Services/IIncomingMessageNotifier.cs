using Murmur.Client;

namespace Murmur.App.Services;

/// <summary>Shows a system notification for messages that arrive while the app is not on screen.</summary>
public interface IIncomingMessageNotifier
{
    void Attach(MurmurClient client);
}

public sealed class NoIncomingMessageNotifier : IIncomingMessageNotifier
{
    public void Attach(MurmurClient client)
    {
    }
}

/// <summary>Whether a Murmur screen is currently visible; notifications are skipped while it is.</summary>
public static class AppVisibility
{
    private static int s_visible;

    public static bool IsVisible => Volatile.Read(ref s_visible) == 1;

    public static void Set(bool visible) => Volatile.Write(ref s_visible, visible ? 1 : 0);
}
