namespace Directo.Networking.Signaling;

public enum SignalingState
{
    Disconnected,
    Connecting,
    Connected,
}

public sealed record PresenceChange(string Topic, int Peers);

public sealed record RelayReceived(string Topic, byte[] Data);

/// <summary>
/// Client view of the signaling service: rendezvous presence plus a small relay used only to
/// negotiate peer-to-peer transports. It never carries chat messages.
/// </summary>
public interface ISignalingChannel
{
    SignalingState State { get; }

    event EventHandler<SignalingState>? StateChanged;

    event EventHandler<PresenceChange>? PresenceChanged;

    event EventHandler<RelayReceived>? RelayReceived;

    /// <summary>Declares the topics a component wants. The effective subscription is the union of all groups.</summary>
    void SetSubscriptions(string group, IReadOnlyCollection<string> topics);

    /// <summary>Last known number of other devices on <paramref name="topic"/> (0 when unknown).</summary>
    int GetPresence(string topic);

    /// <summary>Sends an opaque blob to the other subscribers of <paramref name="topic"/>. Returns false when offline.</summary>
    ValueTask<bool> TryRelayAsync(string topic, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
