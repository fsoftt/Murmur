using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Murmur.Domain.Common;
using Murmur.Domain.Connections;
using Murmur.Domain.Delivery;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Networking.Links;
using Murmur.Networking.Signaling;
using Murmur.Security.Identity;

namespace Murmur.Networking.Sessions;

/// <summary>
/// Keeps one <see cref="ContactConnection"/> per non-blocked contact, subscribes to their rotating
/// rendezvous topics and routes presence changes. Peer-to-peer sessions survive signaling outages.
/// </summary>
public sealed class PeerConnectionManager : IAsyncDisposable
{
    private const string SubscriptionGroup = "contacts";

    private readonly LocalIdentityKeys _keys;
    private readonly IContactRepository _contacts;
    private readonly IConversationRepository _conversations;
    private readonly ConversationSyncSession _sync;
    private readonly ISignalingChannel _signaling;
    private readonly IPeerLinkFactory _links;
    private readonly ChatEvents _events;
    private readonly TimeProvider _time;
    private readonly ConnectionOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Lock _gate = new();
    private readonly Dictionary<ContactId, ContactConnection> _connections = [];
    private readonly Dictionary<string, ContactConnection> _byTopic = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _refreshLoop;

    public PeerConnectionManager(
        LocalIdentityKeys keys,
        IContactRepository contacts,
        IConversationRepository conversations,
        ConversationSyncSession sync,
        ISignalingChannel signaling,
        IPeerLinkFactory links,
        ChatEvents events,
        TimeProvider? time = null,
        ConnectionOptions? options = null,
        ILoggerFactory? loggerFactory = null)
    {
        _keys = keys;
        _contacts = contacts;
        _conversations = conversations;
        _sync = sync;
        _signaling = signaling;
        _links = links;
        _events = events;
        _time = time ?? TimeProvider.System;
        _options = options ?? ConnectionOptions.Default;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _signaling.PresenceChanged += OnPresenceChanged;
        _events.ContactsChanged += OnContactsChanged;
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        _refreshLoop = Task.Run(() => RefreshPeriodicallyAsync(_lifetime.Token), CancellationToken.None);
    }

    public PeerConnectionState GetState(ContactId contactId)
    {
        lock (_gate)
        {
            return _connections.TryGetValue(contactId, out var connection) ? connection.Machine.State : PeerConnectionState.Disconnected;
        }
    }

    /// <summary>Reconciles connections and subscriptions with the contact list and the current epoch.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var contacts = (await _contacts.ListAsync(cancellationToken).ConfigureAwait(false)).Where(c => !c.IsBlocked).ToList();
            var epochs = Rendezvous.ActiveEpochs(_time.GetUtcNow());
            var removed = new List<ContactConnection>();
            var started = new List<ContactConnection>();
            lock (_gate)
            {
                var wanted = contacts.Select(c => c.Id).ToHashSet();
                foreach (var (id, connection) in _connections.Where(kv => !wanted.Contains(kv.Key)).ToList())
                {
                    _connections.Remove(id);
                    removed.Add(connection);
                }

                _byTopic.Clear();
                foreach (var contact in contacts)
                {
                    if (!_connections.TryGetValue(contact.Id, out var connection) || !connection.Contact.Identity.Equals(contact.Identity))
                    {
                        if (connection is not null)
                        {
                            removed.Add(connection);
                        }

                        connection = new ContactConnection(
                            contact, _keys, _contacts, _conversations, _sync, _signaling, _links, _events, _time, _options,
                            _loggerFactory.CreateLogger<ContactConnection>());
                        _connections[contact.Id] = connection;
                        started.Add(connection);
                    }

                    var topics = epochs.Select(e => Rendezvous.ContactTopic(_keys, contact.Identity.StaticKey.Span, e)).ToList();
                    connection.SetTopics(topics);
                    foreach (var topic in topics)
                    {
                        _byTopic[topic] = connection;
                    }
                }

                _signaling.SetSubscriptions(SubscriptionGroup, [.. _byTopic.Keys]);
            }

            foreach (var connection in started)
            {
                connection.Start();
            }

            foreach (var connection in removed)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _signaling.PresenceChanged -= OnPresenceChanged;
        _events.ContactsChanged -= OnContactsChanged;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_refreshLoop is not null)
        {
            try
            {
                await _refreshLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        List<ContactConnection> connections;
        lock (_gate)
        {
            connections = [.. _connections.Values];
            _connections.Clear();
            _byTopic.Clear();
        }

        foreach (var connection in connections)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _signaling.SetSubscriptions(SubscriptionGroup, []);
        _lifetime.Dispose();
    }

    private void OnPresenceChanged(object? sender, PresenceChange change)
    {
        ContactConnection? connection;
        lock (_gate)
        {
            _byTopic.TryGetValue(change.Topic, out connection);
        }

        connection?.RefreshPresence();
    }

    private void OnContactsChanged(object? sender, EventArgs e) =>
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(_lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        });

    private async Task RefreshPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.TopicRefreshInterval, _time);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
