using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Murmur.IntegrationTests.Support;

/// <summary>The real signaling server, in process, with tight limits so abuse paths are testable.</summary>
public sealed class SignalingServerFixture : WebApplicationFactory<Program>
{
    public const int MaxSubscribersPerTopic = 2;

    /// <summary>Every client socket ever opened, wrapped so tests can inspect traffic or kill connections.</summary>
    public ConcurrentBag<RecordingWebSocket> Sockets { get; } = [];

    public Uri Endpoint => new(Server.BaseAddress, "ws");

    public Func<Uri, CancellationToken, Task<WebSocket>> Connector => async (_, cancellationToken) =>
    {
        var client = Server.CreateWebSocketClient();
        var socket = new RecordingWebSocket(await client.ConnectAsync(Endpoint, cancellationToken));
        Sockets.Add(socket);
        return socket;
    };

    public async Task<RecordingWebSocket> ConnectRawAsync() => (RecordingWebSocket)await Connector(Endpoint, CancellationToken.None);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Signaling:MaxSubscribersPerTopic", MaxSubscribersPerTopic.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Signaling:MaxConnectionsPerAddress", "1000");
        builder.UseSetting("Signaling:MessagesPerSecond", "50");
        builder.UseSetting("Signaling:MessageBurst", "50");
    }
}
