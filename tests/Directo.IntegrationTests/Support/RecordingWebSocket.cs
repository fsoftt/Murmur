using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace Directo.IntegrationTests.Support;

/// <summary>Delegating WebSocket that records every frame the client sends or receives.</summary>
public sealed class RecordingWebSocket(WebSocket inner) : WebSocket
{
    public ConcurrentQueue<byte[]> Sent { get; } = new();

    public ConcurrentQueue<byte[]> Received { get; } = new();

    public string AllTraffic => string.Join('\n', Sent.Concat(Received).Select(b => Encoding.UTF8.GetString(b)));

    public override WebSocketCloseStatus? CloseStatus => inner.CloseStatus;

    public override string? CloseStatusDescription => inner.CloseStatusDescription;

    public override WebSocketState State => inner.State;

    public override string? SubProtocol => inner.SubProtocol;

    public override void Abort() => inner.Abort();

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        inner.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose() => inner.Dispose();

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await inner.ReceiveAsync(buffer, cancellationToken);
        Received.Enqueue(buffer.AsSpan(0, result.Count).ToArray());
        return result;
    }

    public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var result = await inner.ReceiveAsync(buffer, cancellationToken);
        Received.Enqueue(buffer[..result.Count].ToArray());
        return result;
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        Sent.Enqueue(buffer.ToArray());
        return inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        Sent.Enqueue(buffer.ToArray());
        return inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
    }
}
