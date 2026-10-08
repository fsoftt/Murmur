namespace Murmur.Domain.Connections;

/// <summary>Connection lifecycle towards one contact (architecture §20).</summary>
public enum PeerConnectionState
{
    /// <summary>Not looking for the peer (stopped, blocked or signaling down).</summary>
    Disconnected,

    /// <summary>Subscribed to the rendezvous topic, waiting for the peer to be online.</summary>
    Discovering,

    /// <summary>Peer seen; establishing transport and the end-to-end handshake.</summary>
    Negotiating,

    /// <summary>Secure session established; messages flow.</summary>
    Connected,

    /// <summary>Session failed or dropped; waiting for the backoff before trying again.</summary>
    Reconnecting,
}

public enum PeerConnectionTrigger
{
    Start,
    PeerSeen,
    Established,
    Failed,
    Lost,
    PeerGone,
    RetryDue,
    Stop,
}

/// <summary>
/// Explicit, table-driven state machine. Every state change goes through <see cref="Fire"/>,
/// so impossible combinations (e.g. "connected while discovering") cannot be represented.
/// </summary>
public sealed class PeerConnectionStateMachine
{
    private static readonly Dictionary<(PeerConnectionState, PeerConnectionTrigger), PeerConnectionState> Transitions = new()
    {
        [(PeerConnectionState.Disconnected, PeerConnectionTrigger.Start)] = PeerConnectionState.Discovering,

        [(PeerConnectionState.Discovering, PeerConnectionTrigger.PeerSeen)] = PeerConnectionState.Negotiating,

        [(PeerConnectionState.Negotiating, PeerConnectionTrigger.Established)] = PeerConnectionState.Connected,
        [(PeerConnectionState.Negotiating, PeerConnectionTrigger.Failed)] = PeerConnectionState.Reconnecting,
        [(PeerConnectionState.Negotiating, PeerConnectionTrigger.PeerGone)] = PeerConnectionState.Discovering,

        [(PeerConnectionState.Connected, PeerConnectionTrigger.Lost)] = PeerConnectionState.Reconnecting,

        [(PeerConnectionState.Reconnecting, PeerConnectionTrigger.RetryDue)] = PeerConnectionState.Negotiating,
        [(PeerConnectionState.Reconnecting, PeerConnectionTrigger.PeerGone)] = PeerConnectionState.Discovering,
    };

    private readonly Lock _gate = new();

    public PeerConnectionState State { get; private set; } = PeerConnectionState.Disconnected;

    public event EventHandler<PeerConnectionState>? StateChanged;

    public bool CanFire(PeerConnectionTrigger trigger)
    {
        lock (_gate)
        {
            return trigger == PeerConnectionTrigger.Stop || Transitions.ContainsKey((State, trigger));
        }
    }

    /// <summary>Applies the trigger if valid in the current state. Returns false (and changes nothing) otherwise.</summary>
    public bool Fire(PeerConnectionTrigger trigger)
    {
        PeerConnectionState next;
        lock (_gate)
        {
            if (trigger == PeerConnectionTrigger.Stop)
            {
                next = PeerConnectionState.Disconnected;
            }
            else if (!Transitions.TryGetValue((State, trigger), out next))
            {
                return false;
            }

            if (next == State)
            {
                return true;
            }

            State = next;
        }

        StateChanged?.Invoke(this, next);
        return true;
    }
}
