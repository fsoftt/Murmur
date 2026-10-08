using Murmur.Domain.Common;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;

namespace Murmur.Domain.UseCases;

/// <summary>
/// Stores a message locally first (transactional outbox) and wakes the delivery session.
/// The message is shown immediately as pending; it is never reported as sent until a session
/// actually transmits it, nor as delivered until the peer acknowledges it.
/// </summary>
public sealed class SendMessage(
    IContactRepository contacts,
    IConversationRepository conversations,
    IMessageRepository messages,
    OutboxSignal outbox,
    ChatEvents events,
    TimeProvider time)
{
    public async Task<Message> ExecuteAsync(ContactId contactId, string body, CancellationToken cancellationToken = default)
    {
        MessageRules.ValidateBody(body);
        var contact = await contacts.GetAsync(contactId, cancellationToken).ConfigureAwait(false)
            ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
        if (contact.IsBlocked)
        {
            throw new MurmurException(MurmurErrorCode.ContactBlocked, "Contact is blocked.");
        }

        var now = MessageRules.Now(time);
        var conversation = await conversations.GetOrCreateForContactAsync(contactId, now, cancellationToken).ConfigureAwait(false);
        var message = await messages
            .AppendOutgoingAsync(new OutgoingMessageDraft(MessageId.New(), conversation.Id, body, now), cancellationToken)
            .ConfigureAwait(false);

        events.OnMessageStored(message);
        outbox.Notify(conversation.Id);
        return message;
    }
}

/// <summary>Moves a failed message back into the outbox.</summary>
public sealed class RetryFailedMessage(IMessageRepository messages, OutboxSignal outbox, ChatEvents events)
{
    public async Task<bool> ExecuteAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default)
    {
        if (!await messages.RequeueAsync(conversationId, id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        events.OnMessageStatusChanged(new MessageStatusChange(conversationId, id, MessageStatus.Pending));
        outbox.Notify(conversationId);
        return true;
    }
}
