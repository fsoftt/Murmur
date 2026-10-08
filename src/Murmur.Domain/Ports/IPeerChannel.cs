using Murmur.Domain.Model;

namespace Murmur.Domain.Ports;

/// <summary>
/// An authenticated, end-to-end encrypted, reliable and ordered channel to one contact.
/// The domain does not know whether it runs over WebRTC, LAN or an in-memory test double.
/// </summary>
public interface IPeerChannel
{
    ValueTask SendMessageAsync(Message message, CancellationToken cancellationToken);

    ValueTask SendAckAsync(MessageId id, CancellationToken cancellationToken);

    /// <summary>Yields events until the channel closes; then completes normally.</summary>
    IAsyncEnumerable<PeerEvent> ReadEventsAsync(CancellationToken cancellationToken);
}

public abstract record PeerEvent;

public sealed record PeerMessageReceived(MessageId Id, long Lamport, DateTimeOffset SentAt, string Body) : PeerEvent;

public sealed record PeerAckReceived(MessageId Id) : PeerEvent;
