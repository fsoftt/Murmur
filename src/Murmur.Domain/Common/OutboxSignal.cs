using System.Collections.Concurrent;
using Murmur.Domain.Model;

namespace Murmur.Domain.Common;

/// <summary>Wakes the delivery session of a conversation when new outgoing messages are stored.</summary>
public sealed class OutboxSignal
{
    private readonly ConcurrentDictionary<ConversationId, AsyncSignal> _signals = new();

    public void Notify(ConversationId conversationId) => For(conversationId).Set();

    public Task WaitAsync(ConversationId conversationId, CancellationToken cancellationToken) =>
        For(conversationId).WaitAsync(cancellationToken);

    private AsyncSignal For(ConversationId conversationId) => _signals.GetOrAdd(conversationId, static _ => new AsyncSignal());
}
