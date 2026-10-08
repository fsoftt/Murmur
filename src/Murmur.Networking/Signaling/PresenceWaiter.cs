namespace Murmur.Networking.Signaling;

internal static class PresenceWaiter
{
    /// <summary>Completes when at least one other device is subscribed to <paramref name="topic"/>.</summary>
    public static async Task WaitForPeerAsync(ISignalingChannel signaling, string topic, CancellationToken cancellationToken)
    {
        var present = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPresence(object? sender, PresenceChange change)
        {
            if (change.Topic == topic && change.Peers > 0)
            {
                present.TrySetResult();
            }
        }

        signaling.PresenceChanged += OnPresence;
        try
        {
            if (signaling.GetPresence(topic) > 0)
            {
                return;
            }

            await present.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            signaling.PresenceChanged -= OnPresence;
        }
    }
}
