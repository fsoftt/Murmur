using Directo.Domain.Model;

namespace Directo.Domain.Ports;

public interface IContactRepository
{
    Task<Contact?> GetAsync(ContactId id, CancellationToken cancellationToken = default);

    Task<Contact?> FindByIdentityKeyAsync(PublicKey identityKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default);

    /// <exception cref="DirectoException">With <see cref="DirectoErrorCode.AlreadyPaired"/> if the identity key exists.</exception>
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);

    Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default);

    /// <summary>Deletes the contact together with its conversation and messages.</summary>
    Task DeleteAsync(ContactId id, CancellationToken cancellationToken = default);
}

public interface IConversationRepository
{
    Task<Conversation> GetOrCreateForContactAsync(ContactId contactId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<Conversation?> GetAsync(ConversationId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationSummary>> ListSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>"Delete for me": removes all local messages, including undelivered ones.</summary>
    Task ClearAsync(ConversationId id, CancellationToken cancellationToken = default);
}

public interface IMessageRepository
{
    /// <summary>Persists a new outgoing message as <see cref="MessageStatus.Pending"/>, assigning the next Lamport time atomically.</summary>
    Task<Message> AppendOutgoingAsync(OutgoingMessageDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an incoming message and advances the conversation clock in one transaction.
    /// Returns false when a message with the same id already exists (idempotent receive).
    /// </summary>
    Task<bool> TryAppendIncomingAsync(Message message, CancellationToken cancellationToken = default);

    Task<Message?> GetAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default);

    /// <summary>The newest <paramref name="limit"/> messages, returned in display order (oldest first).</summary>
    Task<IReadOnlyList<Message>> ListRecentAsync(ConversationId conversationId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Outgoing messages not yet acknowledged (<see cref="MessageStatus.Pending"/> or <see cref="MessageStatus.Sent"/>), in Lamport order.</summary>
    Task<IReadOnlyList<Message>> ListOutboxAsync(ConversationId conversationId, CancellationToken cancellationToken = default);

    /// <summary>Pending → Sent. Returns false if the message is not pending.</summary>
    Task<bool> MarkSentAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default);

    /// <summary>Pending/Sent → Delivered for an outgoing message of this conversation. Returns false otherwise.</summary>
    Task<bool> MarkDeliveredAsync(ConversationId conversationId, MessageId id, DateTimeOffset deliveredAt, CancellationToken cancellationToken = default);

    /// <summary>Failed → Pending, for a manual retry.</summary>
    Task<bool> RequeueAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default);
}

/// <summary>A pairing token this device handed out in a QR code.</summary>
public sealed record PendingInvite(byte[] Token, DateTimeOffset ExpiresAt, PublicKey? ConsumedBy);

public interface IPendingInviteRepository
{
    Task AddAsync(PendingInvite invite, CancellationToken cancellationToken = default);

    Task<PendingInvite?> FindAsync(byte[] token, CancellationToken cancellationToken = default);

    Task MarkConsumedAsync(byte[] token, PublicKey consumedBy, CancellationToken cancellationToken = default);

    Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>Platform secure storage (Android Keystore-backed, iOS Keychain). Never a plain file next to the database.</summary>
public interface ISecretStore
{
    Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default);

    Task SetAsync(string name, byte[] value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Small local key/value settings (e.g. the user's own profile name).</summary>
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}
