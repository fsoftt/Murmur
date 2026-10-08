using System.Net.WebSockets;
using System.Security.Cryptography;
using Directo.IntegrationTests.Support;
using Directo.Networking.Signaling;
using Directo.Protocol.Signaling;

namespace Directo.IntegrationTests;

public sealed class SignalingServerTests(SignalingServerFixture server) : IClassFixture<SignalingServerFixture>
{
    [Fact]
    public async Task Hello_is_answered_with_welcome()
    {
        using var socket = await server.ConnectRawAsync();

        await SendAsync(socket, new HelloMessage(1));

        Assert.IsType<WelcomeMessage>(await ReceiveAsync(socket));
    }

    [Fact]
    public async Task Connection_must_start_with_hello()
    {
        using var socket = await server.ConnectRawAsync();

        await SendAsync(socket, new SubscribeMessage([NewTopic()]));

        Assert.Null(await ReceiveAsync(socket));
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, socket.CloseStatus);
    }

    [Fact]
    public async Task Presence_reports_other_subscribers_only()
    {
        var topic = NewTopic();
        using var a = await HandshakeAsync();
        using var b = await HandshakeAsync();

        await SendAsync(a, new SubscribeMessage([topic]));
        Assert.Equal(new PresenceMessage(topic, 0), await ReceiveAsync(a));

        await SendAsync(b, new SubscribeMessage([topic]));
        Assert.Equal(new PresenceMessage(topic, 1), await ReceiveAsync(a));
        Assert.Equal(new PresenceMessage(topic, 1), await ReceiveAsync(b));

        await a.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        Assert.Equal(new PresenceMessage(topic, 0), await ReceiveAsync(b));
    }

    [Fact]
    public async Task Relay_reaches_the_other_subscriber_but_not_the_sender()
    {
        var topic = NewTopic();
        using var a = await HandshakeAsync();
        using var b = await HandshakeAsync();
        await SubscribeAndDrainAsync(a, topic);
        await SubscribeAndDrainAsync(b, topic);
        await ReceiveAsync(a); // presence update caused by b

        var data = SignalingCodec.EncodeRelayData([1, 2, 3]);
        await SendAsync(a, new RelayMessage(topic, data));

        Assert.Equal(new RelayMessage(topic, data), await ReceiveAsync(b));
        await SendAsync(a, new PingMessage());
        Assert.IsType<PongMessage>(await ReceiveAsync(a));
    }

    [Fact]
    public async Task Relay_requires_a_subscription()
    {
        using var a = await HandshakeAsync();

        await SendAsync(a, new RelayMessage(NewTopic(), "AQID"));

        Assert.Equal(new ErrorMessage(SignalingErrorCodes.NotSubscribed), await ReceiveAsync(a));
    }

    [Fact]
    public async Task Topics_have_a_member_limit()
    {
        var topic = NewTopic();
        var sockets = new List<WebSocket>();
        for (var i = 0; i < SignalingServerFixture.MaxSubscribersPerTopic; i++)
        {
            var socket = await HandshakeAsync();
            sockets.Add(socket);
            await SendAsync(socket, new SubscribeMessage([topic]));
        }

        using var extra = await HandshakeAsync();
        await SendAsync(extra, new SubscribeMessage([topic]));

        Assert.Equal(new ErrorMessage(SignalingErrorCodes.TopicFull), await ReceiveAsync(extra));
        sockets.ForEach(s => s.Dispose());
    }

    [Fact]
    public async Task Flooding_is_rate_limited()
    {
        using var socket = await HandshakeAsync();

        for (var i = 0; i < 200; i++)
        {
            await SendAsync(socket, new PingMessage());
        }

        var replies = new List<SignalingMessage?>();
        for (var i = 0; i < 200; i++)
        {
            replies.Add(await ReceiveAsync(socket));
        }

        Assert.Contains(new ErrorMessage(SignalingErrorCodes.RateLimited), replies);
    }

    [Fact]
    public async Task Oversized_frames_close_the_connection()
    {
        using var socket = await HandshakeAsync();

        await socket.SendAsync(new byte[SignalingCodec.MaxFrameBytes + 10], WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Null(await ReceiveAsync(socket));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatus);
    }

    [Fact]
    public async Task Malformed_json_is_rejected_without_closing()
    {
        using var socket = await HandshakeAsync();

        await socket.SendAsync("{nope"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Equal(new ErrorMessage(SignalingErrorCodes.BadRequest), await ReceiveAsync(socket));
        await SendAsync(socket, new PingMessage());
        Assert.IsType<PongMessage>(await ReceiveAsync(socket));
    }

    [Fact]
    public async Task Client_reconnects_and_restores_subscriptions()
    {
        var topic = NewTopic();
        await using var client = new SignalingClient(server.Endpoint, server.Connector, backoff: new Domain.Common.Backoff(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100)));
        client.SetSubscriptions("test", [topic]);
        client.Start();
        using var peer = await HandshakeAsync();
        await SubscribeAndDrainAsync(peer, topic);
        await Eventually.TrueAsync(() => client.GetPresence(topic) == 1, "client sees the peer");

        var first = server.Sockets.Last(s => s.AllTraffic.Contains(topic, StringComparison.Ordinal) && s != peer);
        first.Abort();

        await Eventually.TrueAsync(() => client.State == SignalingState.Disconnected || client.GetPresence(topic) == 0, "client notices the drop");
        await Eventually.TrueAsync(() => client.State == SignalingState.Connected && client.GetPresence(topic) == 1, "client is back with its subscription");
    }

    private async Task<WebSocket> HandshakeAsync()
    {
        var socket = await server.ConnectRawAsync();
        await SendAsync(socket, new HelloMessage(1));
        Assert.IsType<WelcomeMessage>(await ReceiveAsync(socket));
        return socket;
    }

    private static async Task SubscribeAndDrainAsync(WebSocket socket, string topic)
    {
        await SendAsync(socket, new SubscribeMessage([topic]));
        Assert.IsType<PresenceMessage>(await ReceiveAsync(socket));
    }

    private static string NewTopic() => RendezvousTopicFormat.Encode(RandomNumberGenerator.GetBytes(32));

    private static Task SendAsync(WebSocket socket, SignalingMessage message) =>
        socket.SendAsync(SignalingCodec.Serialize(message), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<SignalingMessage?> ReceiveAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[64 * 1024];
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        Assert.True(SignalingCodec.TryDeserialize(buffer.AsSpan(0, result.Count), out var message));
        return message;
    }
}
