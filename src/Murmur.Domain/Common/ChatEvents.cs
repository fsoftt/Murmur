using Murmur.Domain.Connections;
using Murmur.Domain.Model;

namespace Murmur.Domain.Common;

/// <summary>
/// In-process notifications for the presentation layer. Handlers run synchronously on the
/// publishing thread and must not block; view models marshal to the UI thread themselves.
/// </summary>
public sealed class ChatEvents
{
    public event EventHandler<Message>? MessageStored;

    public event EventHandler<MessageStatusChange>? MessageStatusChanged;

    public event EventHandler? ContactsChanged;

    public event EventHandler<ConnectionStateChange>? ConnectionStateChanged;

    public void OnMessageStored(Message message) => MessageStored?.Invoke(this, message);

    public void OnMessageStatusChanged(MessageStatusChange change) => MessageStatusChanged?.Invoke(this, change);

    public void OnContactsChanged() => ContactsChanged?.Invoke(this, EventArgs.Empty);

    public void OnConnectionStateChanged(ConnectionStateChange change) => ConnectionStateChanged?.Invoke(this, change);
}

public sealed record MessageStatusChange(ConversationId ConversationId, MessageId MessageId, MessageStatus Status);

public sealed record ConnectionStateChange(ContactId ContactId, PeerConnectionState State);
