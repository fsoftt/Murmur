using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Directo.Domain.Model;
using Directo.Domain.Ports;

namespace Directo.Core.Tests.Support;

/// <summary>Two connected in-memory channels with fault injection, standing in for a secure session.</summary>
public sealed class FakePeerChannel : IPeerChannel
{
    private readonly Channel<PeerEvent> _inbox = Channel.CreateUnbounded<PeerEvent>();
    private FakePeerChannel _remote = null!;

    /// <summary>Return false to silently drop an outgoing event (simulates loss before persistence on the other side).</summary>
    public Func<PeerEvent, bool> Filter { get; set; } = _ => true;

    public int MessagesSent;

    public static (FakePeerChannel A, FakePeerChannel B) CreatePair()
    {
        var a = new FakePeerChannel();
        var b = new FakePeerChannel();
        a._remote = b;
        b._remote = a;
        return (a, b);
    }

    public ValueTask SendMessageAsync(Message message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref MessagesSent);
        return Deliver(new PeerMessageReceived(message.Id, message.Lamport, message.CreatedAt, message.Body));
    }

    public ValueTask SendAckAsync(MessageId id, CancellationToken cancellationToken) => Deliver(new PeerAckReceived(id));

    public async IAsyncEnumerable<PeerEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _inbox.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    /// <summary>Simulates the connection dropping: both sides' reads complete.</summary>
    public void Close()
    {
        _inbox.Writer.TryComplete();
        _remote._inbox.Writer.TryComplete();
    }

    private ValueTask Deliver(PeerEvent peerEvent)
    {
        if (Filter(peerEvent))
        {
            _remote._inbox.Writer.TryWrite(peerEvent);
        }

        return ValueTask.CompletedTask;
    }
}
