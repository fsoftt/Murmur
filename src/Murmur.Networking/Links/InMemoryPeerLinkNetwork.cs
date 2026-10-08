using System.Threading.Channels;
using Murmur.Networking.Signaling;

namespace Murmur.Networking.Links;

/// <summary>
/// A simulated network for tests and local development: devices that ask for the same topic with
/// opposite roles get connected. Supports simulating unreachable peers (e.g. symmetric NAT
/// without TURN) and dropping every open link.
/// </summary>
public sealed class InMemoryPeerLinkNetwork : IPeerLinkFactory
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Topic, bool Initiator), TaskCompletionSource<InMemoryPeerLink>> _waiting = new();
    private readonly List<InMemoryPeerLink> _open = [];

    private bool _reachable = true;

    /// <summary>When false, connection attempts hang until cancelled, as with a failed ICE negotiation.</summary>
    public bool Reachable
    {
        get
        {
            lock (_gate)
            {
                return _reachable;
            }
        }

        set
        {
            lock (_gate)
            {
                _reachable = value;
                if (!value)
                {
                    return;
                }

                // Attempts that were waiting while unreachable can now meet.
                foreach (var (topic, _) in _waiting.Keys.Where(k => k.Initiator).ToList())
                {
                    if (_waiting.Remove((topic, false), out var responder) && _waiting.Remove((topic, true), out var initiatorSource))
                    {
                        var (a, b) = InMemoryPeerLink.CreatePair(Forget);
                        _open.Add(a);
                        _open.Add(b);
                        ConnectionsEstablished++;
                        initiatorSource.TrySetResult(a);
                        responder.TrySetResult(b);
                    }
                }
            }
        }
    }

    public int ConnectionsEstablished { get; private set; }

    public async Task<IPeerLink> ConnectAsync(PeerLinkRequest request, ISignalingChannel signaling, CancellationToken cancellationToken)
    {
        TaskCompletionSource<InMemoryPeerLink> mine;
        lock (_gate)
        {
            if (_reachable && _waiting.Remove((request.Topic, !request.IsInitiator), out var other))
            {
                var (a, b) = InMemoryPeerLink.CreatePair(Forget);
                _open.Add(a);
                _open.Add(b);
                ConnectionsEstablished++;
                other.TrySetResult(b);
                return a;
            }

            mine = new TaskCompletionSource<InMemoryPeerLink>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_waiting.Remove((request.Topic, request.IsInitiator), out var stale))
            {
                stale.TrySetCanceled(CancellationToken.None);
            }

            _waiting[(request.Topic, request.IsInitiator)] = mine;
        }

        using var registration = cancellationToken.Register(() =>
        {
            lock (_gate)
            {
                if (_waiting.TryGetValue((request.Topic, request.IsInitiator), out var current) && current == mine)
                {
                    _waiting.Remove((request.Topic, request.IsInitiator));
                }
            }

            mine.TrySetCanceled(cancellationToken);
        });
        return await mine.Task.ConfigureAwait(false);
    }

    /// <summary>Simulates a network change that kills every open peer-to-peer connection.</summary>
    public void DropAllLinks()
    {
        List<InMemoryPeerLink> open;
        lock (_gate)
        {
            open = [.. _open];
        }

        foreach (var link in open)
        {
            link.Close();
        }
    }

    private void Forget(InMemoryPeerLink link)
    {
        lock (_gate)
        {
            _open.Remove(link);
        }
    }
}

public sealed class InMemoryPeerLink : IPeerLink
{
    private readonly Channel<byte[]> _inbox = Channel.CreateUnbounded<byte[]>();
    private readonly Action<InMemoryPeerLink> _onClosed;
    private InMemoryPeerLink _remote = null!;

    private InMemoryPeerLink(Action<InMemoryPeerLink> onClosed)
    {
        _onClosed = onClosed;
    }

    internal static (InMemoryPeerLink A, InMemoryPeerLink B) CreatePair(Action<InMemoryPeerLink> onClosed)
    {
        var a = new InMemoryPeerLink(onClosed);
        var b = new InMemoryPeerLink(onClosed);
        a._remote = b;
        b._remote = a;
        return (a, b);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (message.Length > IPeerLink.MaxMessageSize)
        {
            throw new ArgumentException("Message too large for the link.", nameof(message));
        }

        if (!_remote._inbox.Writer.TryWrite(message.ToArray()))
        {
            throw new IOException("Link closed.");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _inbox.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void Close()
    {
        _inbox.Writer.TryComplete();
        _remote._inbox.Writer.TryComplete();
        _onClosed(this);
        _onClosed(_remote);
    }

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }
}
