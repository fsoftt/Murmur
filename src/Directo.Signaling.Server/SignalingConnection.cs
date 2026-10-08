using System.Net.WebSockets;
using System.Threading.Channels;
using Directo.Protocol.Signaling;

namespace Directo.Signaling.Server;

/// <summary>One client WebSocket. Outbound messages go through a bounded queue drained by a single writer.</summary>
public sealed class SignalingConnection
{
    private readonly WebSocket _socket;
    private readonly Channel<byte[]> _outbound;

    internal SignalingConnection(WebSocket socket, int maxQueue)
    {
        _socket = socket;
        _outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(maxQueue) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    }

    /// <summary>Guarded by the hub's lock.</summary>
    internal HashSet<string> Topics { get; } = new(StringComparer.Ordinal);

    internal bool IsOverloaded { get; private set; }

    /// <summary>Queues a message. A client that cannot keep up is marked overloaded and gets disconnected.</summary>
    public void Enqueue(SignalingMessage message)
    {
        if (!_outbound.Writer.TryWrite(SignalingCodec.Serialize(message)))
        {
            IsOverloaded = true;
            _outbound.Writer.TryComplete();
        }
    }

    internal void CompleteOutbound() => _outbound.Writer.TryComplete();

    internal async Task RunWriterAsync(CancellationToken cancellationToken)
    {
        await foreach (var payload in _outbound.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_socket.State != WebSocketState.Open)
            {
                return;
            }

            await _socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
    }
}
