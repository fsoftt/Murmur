using Directo.Networking.Signaling;

namespace Directo.Networking.Links;

/// <summary>
/// Placeholder used by release builds until the WebRTC transport lands (ADR-006): no direct path
/// can be established, so every attempt waits until it times out and messages stay pending.
/// </summary>
public sealed class UnavailablePeerLinkFactory : IPeerLinkFactory
{
    public async Task<IPeerLink> ConnectAsync(PeerLinkRequest request, ISignalingChannel signaling, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        throw new OperationCanceledException(cancellationToken);
    }
}
