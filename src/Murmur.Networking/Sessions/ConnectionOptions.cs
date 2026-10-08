using Murmur.Domain.Common;

namespace Murmur.Networking.Sessions;

public sealed record ConnectionOptions
{
    /// <summary>Time allowed to establish the peer link and run the handshake.</summary>
    public TimeSpan EstablishTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>Delay between attempts while the peer is online but unreachable or after a drop.</summary>
    public Backoff Reconnect { get; init; } = new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2));

    /// <summary>How often rotating rendezvous topics are recomputed.</summary>
    public TimeSpan TopicRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    public static ConnectionOptions Default { get; } = new();
}
