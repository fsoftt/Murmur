using Murmur.Domain.Common;
using Murmur.Domain.Connections;
using Murmur.Domain.Delivery;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Networking.Links;
using Murmur.Networking.Secure;
using Murmur.Networking.Signaling;
using Murmur.Security.Identity;
using Microsoft.Extensions.Logging;

namespace Murmur.Networking.Sessions;

/// <summary>
/// Owns the connection lifecycle towards one contact: waits until the contact is present on a
/// shared rendezvous topic, establishes a link and a Noise_KK session, runs message delivery,
/// and retries with backoff when anything fails. All state changes go through the state machine.
/// </summary>
internal sealed partial class ContactConnection : IAsyncDisposable
{
    private readonly LocalIdentityKeys _keys;
    private readonly IContactRepository _contacts;
    private readonly IConversationRepository _conversations;
    private readonly ConversationSyncSession _sync;
    private readonly ISignalingChannel _signaling;
    private readonly IPeerLinkFactory _links;
    private readonly TimeProvider _time;
    private readonly ConnectionOptions _options;
    private readonly ILogger _logger;
    private readonly AsyncSignal _wake = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly bool _isInitiator;
    private volatile IReadOnlyList<string> _topics = [];
    private volatile string? _presentTopic;
    private Task? _loop;

    public ContactConnection(
        Contact contact,
        LocalIdentityKeys keys,
        IContactRepository contacts,
        IConversationRepository conversations,
        ConversationSyncSession sync,
        ISignalingChannel signaling,
        IPeerLinkFactory links,
        ChatEvents events,
        TimeProvider time,
        ConnectionOptions options,
        ILogger logger)
    {
        Contact = contact;
        _keys = keys;
        _contacts = contacts;
        _conversations = conversations;
        _sync = sync;
        _signaling = signaling;
        _links = links;
        _time = time;
        _options = options;
        _logger = logger;
        _isInitiator = KeyOrdering.IsInitiator(keys.StaticPublicKey, contact.Identity.StaticKey.Span);
        Machine.StateChanged += (_, state) => events.OnConnectionStateChanged(new ConnectionStateChange(contact.Id, state));
    }

    public Contact Contact { get; }

    public PeerConnectionStateMachine Machine { get; } = new();

    public IReadOnlyList<string> Topics => _topics;

    public void Start() => _loop ??= Task.Run(() => RunAsync(_lifetime.Token));

    public void SetTopics(IReadOnlyList<string> topics)
    {
        _topics = topics;
        RefreshPresence();
    }

    /// <summary>
    /// Re-evaluates presence. Both devices pick the smallest topic on which the other is present,
    /// so they agree on where to negotiate even when subscribed to two epochs around midnight.
    /// </summary>
    public void RefreshPresence()
    {
        _presentTopic = _topics.Where(t => _signaling.GetPresence(t) > 0).Order(StringComparer.Ordinal).FirstOrDefault();
        _wake.Set();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        _lifetime.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        Machine.Fire(PeerConnectionTrigger.Start);
        var attempt = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var topic = _presentTopic;
                if (topic is null)
                {
                    Machine.Fire(PeerConnectionTrigger.PeerGone);
                    await _wake.WaitAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                Machine.Fire(Machine.State == PeerConnectionState.Reconnecting ? PeerConnectionTrigger.RetryDue : PeerConnectionTrigger.PeerSeen);
                if (await RunSessionAsync(topic, cancellationToken).ConfigureAwait(false))
                {
                    attempt = 0;
                }

                var delay = _options.Reconnect.Delay(attempt++, Random.Shared.NextDouble());
                await WaitForRetryAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Machine.Fire(PeerConnectionTrigger.Stop);
        }
    }

    /// <summary>Returns true if a session was established (even if it later dropped).</summary>
    private async Task<bool> RunSessionAsync(string topic, CancellationToken cancellationToken)
    {
        var established = false;
        try
        {
            var conversation = await _conversations.GetOrCreateForContactAsync(Contact.Id, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            using var establish = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            establish.CancelAfter(_options.EstablishTimeout);

            await using var link = await _links.ConnectAsync(new PeerLinkRequest(topic, _isInitiator), _signaling, establish.Token).ConfigureAwait(false);
            await using var session = await SecureHandshake
                .EstablishContactSessionAsync(link, _isInitiator, _keys, Contact.Identity.StaticKey, establish.Token)
                .ConfigureAwait(false);

            // The contact may have been blocked or deleted after this connection started (the
            // manager reacts asynchronously). Never exchange messages with it in that window.
            if (!await IsStillAllowedAsync(cancellationToken).ConfigureAwait(false))
            {
                LogSessionRefused(_logger);
                throw new MurmurException(MurmurErrorCode.ContactBlocked, "Contact blocked or removed.");
            }

            established = true;
            Machine.Fire(PeerConnectionTrigger.Established);
            LogSessionEstablished(_logger, session.ProtocolVersion);
            await _sync.RunAsync(conversation, session, cancellationToken, IsStillAllowedAsync).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Only the failure category is logged: no keys, topics or message content.
            LogSessionFailed(_logger, established, ex is MurmurException d ? d.Code.ToString() : ex.GetType().Name);
        }

        Machine.Fire(established ? PeerConnectionTrigger.Lost : PeerConnectionTrigger.Failed);
        return established;
    }

    private async Task<bool> IsStillAllowedAsync(CancellationToken cancellationToken) =>
        await _contacts.GetAsync(Contact.Id, cancellationToken).ConfigureAwait(false) is { IsBlocked: false } current
        && current.Identity.Equals(Contact.Identity);

    private async Task WaitForRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
        if (_presentTopic is null)
        {
            Machine.Fire(PeerConnectionTrigger.PeerGone);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Secure session established (protocol v{Version})")]
    private static partial void LogSessionEstablished(ILogger logger, int version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secure session refused: contact blocked or removed")]
    private static partial void LogSessionRefused(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Peer session ended (established: {Established}, reason: {Reason})")]
    private static partial void LogSessionFailed(ILogger logger, bool established, string reason);
}
