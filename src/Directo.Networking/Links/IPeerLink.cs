using Directo.Networking.Signaling;

namespace Directo.Networking.Links;

/// <summary>
/// A reliable, ordered, message-oriented pipe to a peer device (architecture §34). It carries
/// only Noise handshake and transport messages, so it needs no confidentiality of its own.
/// Implementations: WebRTC DataChannel (production), LAN or Bluetooth in the future, and an
/// in-memory network for tests.
/// </summary>
public interface IPeerLink : IAsyncDisposable
{
    /// <summary>Maximum size of a single message (one Noise message).</summary>
    public const int MaxMessageSize = 65535;

    ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken);

    /// <summary>Next message, or null once the link is closed.</summary>
    ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken);
}

/// <param name="Topic">Rendezvous topic both devices are subscribed to; also scopes transport negotiation.</param>
/// <param name="IsInitiator">Exactly one side initiates; the other waits for it.</param>
public sealed record PeerLinkRequest(string Topic, bool IsInitiator);

public interface IPeerLinkFactory
{
    /// <summary>Establishes a direct link. May use <paramref name="signaling"/> to exchange negotiation blobs.</summary>
    Task<IPeerLink> ConnectAsync(PeerLinkRequest request, ISignalingChannel signaling, CancellationToken cancellationToken);
}
