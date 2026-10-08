using Directo.Domain.Common;
using Directo.Domain.Model;
using Directo.Domain.Ports;

namespace Directo.Domain.Delivery;

public sealed record DeliveryOptions
{
    /// <summary>Retransmission schedule for messages transmitted without an ACK.</summary>
    public Backoff Retransmission { get; init; } = new(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));

    public static DeliveryOptions Default { get; } = new();
}

/// <summary>
/// Runs the message protocol over one established secure session (architecture §11-12):
/// flushes the local outbox, retransmits unacknowledged messages, stores incoming messages
/// idempotently and acknowledges them only after they are persisted.
/// </summary>
public sealed class ConversationSyncSession(
    IMessageRepository messages,
    OutboxSignal outbox,
    ChatEvents events,
    TimeProvider time,
    DeliveryOptions? options = null)
{
    private readonly DeliveryOptions _options = options ?? DeliveryOptions.Default;

    /// <summary>Runs until the channel closes or <paramref name="cancellationToken"/> fires.</summary>
    public async Task RunAsync(Conversation conversation, IPeerChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(channel);
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receive = ReceiveLoopAsync(conversation.Id, channel, session.Token);
        var send = SendLoopAsync(conversation.Id, channel, session.Token);

        var first = await Task.WhenAny(receive, send).ConfigureAwait(false);
        await session.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(receive, send).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!first.IsFaulted)
        {
            // The other loop was stopped because the session ended.
        }

        // Surface the failure that ended the session, if any (cancellation is a normal end).
        if (first.IsFaulted)
        {
            await first.ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(ConversationId conversationId, IPeerChannel channel, CancellationToken cancellationToken)
    {
        await foreach (var peerEvent in channel.ReadEventsAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (peerEvent)
            {
                case PeerMessageReceived received:
                    await StoreIncomingAsync(conversationId, received, cancellationToken).ConfigureAwait(false);

                    // ACK only after the message is durably stored, and also for duplicates:
                    // the sender may have lost our previous ACK.
                    await channel.SendAckAsync(received.Id, cancellationToken).ConfigureAwait(false);
                    break;
                case PeerAckReceived ack:
                    var now = MessageRules.Now(time);
                    if (await messages.MarkDeliveredAsync(conversationId, ack.Id, now, cancellationToken).ConfigureAwait(false))
                    {
                        events.OnMessageStatusChanged(new MessageStatusChange(conversationId, ack.Id, MessageStatus.Delivered));
                    }

                    break;
            }
        }
    }

    private async Task StoreIncomingAsync(ConversationId conversationId, PeerMessageReceived received, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(received.Body))
        {
            return;
        }

        var message = new Message(
            received.Id,
            conversationId,
            MessageDirection.Incoming,
            received.Body,
            MessageStatus.Received,
            received.Lamport,
            received.SentAt,
            ReceivedAt: MessageRules.Now(time));
        if (await messages.TryAppendIncomingAsync(message, cancellationToken).ConfigureAwait(false))
        {
            events.OnMessageStored(message);
        }
    }

    private async Task SendLoopAsync(ConversationId conversationId, IPeerChannel channel, CancellationToken cancellationToken)
    {
        var inFlight = new Dictionary<MessageId, (DateTimeOffset ResendAt, int Attempts)>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var outboxMessages = await messages.ListOutboxAsync(conversationId, cancellationToken).ConfigureAwait(false);
            var now = MessageRules.Now(time);
            var stillOutstanding = new HashSet<MessageId>();
            foreach (var message in outboxMessages)
            {
                stillOutstanding.Add(message.Id);
                if (inFlight.TryGetValue(message.Id, out var state) && now < state.ResendAt)
                {
                    continue;
                }

                await channel.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                if (message.Status == MessageStatus.Pending
                    && await messages.MarkSentAsync(conversationId, message.Id, cancellationToken).ConfigureAwait(false))
                {
                    events.OnMessageStatusChanged(new MessageStatusChange(conversationId, message.Id, MessageStatus.Sent));
                }

                var attempts = state.Attempts;
                inFlight[message.Id] = (now + _options.Retransmission.Delay(attempts, Random.Shared.NextDouble()), attempts + 1);
            }

            foreach (var acknowledged in inFlight.Keys.Where(id => !stillOutstanding.Contains(id)).ToList())
            {
                inFlight.Remove(acknowledged);
            }

            await WaitForWorkAsync(conversationId, inFlight, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForWorkAsync(
        ConversationId conversationId,
        Dictionary<MessageId, (DateTimeOffset ResendAt, int Attempts)> inFlight,
        CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var newMessages = outbox.WaitAsync(conversationId, wait.Token);
        Task? resendDue = null;
        if (inFlight.Count > 0)
        {
            var delay = inFlight.Values.Min(v => v.ResendAt) - time.GetUtcNow();
            resendDue = Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, time, wait.Token);
        }

        var completed = resendDue is null
            ? await Task.WhenAny(newMessages).ConfigureAwait(false)
            : await Task.WhenAny(newMessages, resendDue).ConfigureAwait(false);
        await wait.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await completed.ConfigureAwait(false);
    }
}
