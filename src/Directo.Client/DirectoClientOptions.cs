using System.Net.WebSockets;
using Directo.Domain.Delivery;
using Directo.Networking.Sessions;

namespace Directo.Client;

public sealed record DirectoClientOptions
{
    /// <summary>Path of the encrypted SQLite database.</summary>
    public required string DatabasePath { get; init; }

    /// <summary>WebSocket endpoint of the signaling service, e.g. wss://signal.example.org/ws.</summary>
    public required Uri SignalingEndpoint { get; init; }

    /// <summary>Optional WebSocket factory (tests, custom TLS pinning).</summary>
    public Func<Uri, CancellationToken, Task<WebSocket>>? SignalingConnector { get; init; }

    public DeliveryOptions Delivery { get; init; } = DeliveryOptions.Default;

    public ConnectionOptions Connections { get; init; } = ConnectionOptions.Default;
}
