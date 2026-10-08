namespace Directo.Domain.Model;

public enum MessageDirection
{
    Outgoing = 0,
    Incoming = 1,
}

/// <summary>
/// Local delivery state. "Sent" means bytes were handed to an established session, never that the
/// peer stored the message; only <see cref="Delivered"/> (an ACK after persistence) means that.
/// </summary>
public enum MessageStatus
{
    /// <summary>Stored only on this device, waiting for both devices to be online.</summary>
    Pending = 0,

    /// <summary>Transmitted in a secure session, waiting for the peer's ACK.</summary>
    Sent = 1,

    /// <summary>The peer confirmed it durably stored the message.</summary>
    Delivered = 2,

    /// <summary>Will not be delivered automatically (e.g. rejected by the peer).</summary>
    Failed = 3,

    /// <summary>Incoming message stored on this device.</summary>
    Received = 4,
}

public sealed record Message(
    MessageId Id,
    ConversationId ConversationId,
    MessageDirection Direction,
    string Body,
    MessageStatus Status,
    long Lamport,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReceivedAt = null,
    DateTimeOffset? DeliveredAt = null)
{
    public bool IsInOutbox => Direction == MessageDirection.Outgoing && Status is MessageStatus.Pending or MessageStatus.Sent;
}

/// <summary>What the sender knows before the repository assigns the logical clock.</summary>
public sealed record OutgoingMessageDraft(MessageId Id, ConversationId ConversationId, string Body, DateTimeOffset CreatedAt);
