using System.Collections.Concurrent;

namespace Directo.Signaling.Server;

public enum SubscribeResult
{
    Subscribed,
    AlreadySubscribed,
    TopicFull,
    TooManyTopics,
}

/// <summary>
/// Membership of rendezvous topics. Kept behind an interface so a multi-node deployment can
/// replace it with a pub/sub backed implementation without touching the protocol handling.
/// </summary>
public interface ITopicHub
{
    SubscribeResult Subscribe(SignalingConnection connection, string topic);

    bool Unsubscribe(SignalingConnection connection, string topic);

    /// <summary>Removes the connection from all its topics and returns them.</summary>
    IReadOnlyList<string> RemoveConnection(SignalingConnection connection);

    IReadOnlyList<SignalingConnection> Members(string topic);
}

/// <summary>Single-process implementation. Holds nothing but live connections; nothing is persisted.</summary>
public sealed class InMemoryTopicHub(SignalingOptions options) : ITopicHub
{
    private readonly ConcurrentDictionary<string, HashSet<SignalingConnection>> _topics = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public SubscribeResult Subscribe(SignalingConnection connection, string topic)
    {
        lock (_gate)
        {
            if (connection.Topics.Contains(topic))
            {
                return SubscribeResult.AlreadySubscribed;
            }

            if (connection.Topics.Count >= options.MaxTopicsPerConnection)
            {
                return SubscribeResult.TooManyTopics;
            }

            var members = _topics.GetOrAdd(topic, static _ => []);
            if (members.Count >= options.MaxSubscribersPerTopic)
            {
                return SubscribeResult.TopicFull;
            }

            members.Add(connection);
            connection.Topics.Add(topic);
            return SubscribeResult.Subscribed;
        }
    }

    public bool Unsubscribe(SignalingConnection connection, string topic)
    {
        lock (_gate)
        {
            return RemoveLocked(connection, topic);
        }
    }

    public IReadOnlyList<string> RemoveConnection(SignalingConnection connection)
    {
        lock (_gate)
        {
            var topics = connection.Topics.ToList();
            foreach (var topic in topics)
            {
                RemoveLocked(connection, topic);
            }

            return topics;
        }
    }

    public IReadOnlyList<SignalingConnection> Members(string topic)
    {
        lock (_gate)
        {
            return _topics.TryGetValue(topic, out var members) ? [.. members] : [];
        }
    }

    private bool RemoveLocked(SignalingConnection connection, string topic)
    {
        if (!connection.Topics.Remove(topic))
        {
            return false;
        }

        if (_topics.TryGetValue(topic, out var members))
        {
            members.Remove(connection);
            if (members.Count == 0)
            {
                _topics.TryRemove(topic, out _);
            }
        }

        return true;
    }
}
