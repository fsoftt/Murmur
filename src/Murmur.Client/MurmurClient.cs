using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Murmur.Domain.Common;
using Murmur.Domain.Connections;
using Murmur.Domain.Delivery;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Domain.UseCases;
using Murmur.Networking.Links;
using Murmur.Networking.Pairing;
using Murmur.Networking.Sessions;
using Murmur.Networking.Signaling;
using Murmur.Security.Identity;
using Murmur.Storage.Database;
using Murmur.Storage.Repositories;

namespace Murmur.Client;

/// <summary>
/// Composition root of a Murmur device: identity, encrypted storage, signaling, peer sessions,
/// delivery and pairing. The presentation layer talks only to this facade and its use cases.
/// </summary>
public sealed class MurmurClient : IAsyncDisposable
{
    private const string ProfileNameSetting = "profile.name";
    private const string OnboardedSetting = "onboarding.done";

    private readonly LocalIdentityKeys _keys;
    private readonly SqliteDatabase _database;
    private readonly SignalingClient _signaling;
    private readonly PeerConnectionManager _connections;
    private readonly IContactRepository _contacts;
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly ISettingsRepository _settings;
    private readonly SendMessage _sendMessage;
    private readonly RetryFailedMessage _retryFailed;
    private readonly TimeProvider _time;
    private bool _started;

    private MurmurClient(
        LocalIdentityKeys keys,
        SqliteDatabase database,
        MurmurClientOptions options,
        IPeerLinkFactory links,
        TimeProvider time,
        ILoggerFactory loggerFactory)
    {
        _keys = keys;
        _database = database;
        _time = time;
        _contacts = new SqliteContactRepository(database);
        _conversations = new SqliteConversationRepository(database);
        _messages = new SqliteMessageRepository(database);
        _settings = new SqliteSettingsRepository(database);
        var invites = new SqlitePendingInviteRepository(database);
        var outbox = new OutboxSignal();

        _signaling = new SignalingClient(options.SignalingEndpoint, options.SignalingConnector, time, loggerFactory.CreateLogger<SignalingClient>());
        var sync = new ConversationSyncSession(_messages, outbox, Events, time, options.Delivery);
        _connections = new PeerConnectionManager(keys, _contacts, _conversations, sync, _signaling, links, Events, time, options.Connections, loggerFactory);
        Pairing = new PairingService(keys, _contacts, _conversations, invites, _signaling, links, Events, time, loggerFactory.CreateLogger<PairingService>());
        Contacts = new ManageContacts(_contacts, _conversations, Events);
        _sendMessage = new SendMessage(_contacts, _conversations, _messages, outbox, Events, time);
        _retryFailed = new RetryFailedMessage(_messages, outbox, Events);
        _signaling.StateChanged += (_, state) => SignalingStateChanged?.Invoke(this, state);
    }

    public ChatEvents Events { get; } = new();

    public ManageContacts Contacts { get; }

    public PairingService Pairing { get; }

    /// <summary>This device's public identity key.</summary>
    public PublicKey IdentityKey => new(_keys.IdentityPublicKey);

    public SignalingState SignalingState => _signaling.State;

    public event EventHandler<SignalingState>? SignalingStateChanged;

    /// <summary>Loads (or creates on first run) the identity and the encrypted database.</summary>
    public static async Task<MurmurClient> OpenAsync(
        MurmurClientOptions options,
        ISecretStore secrets,
        IPeerLinkFactory links,
        TimeProvider? time = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(links);
        var keys = await IdentityStore.LoadOrCreateAsync(secrets, cancellationToken).ConfigureAwait(false);
        var databaseKey = await DatabaseKeyProvider.GetOrCreateAsync(secrets, cancellationToken).ConfigureAwait(false);
        SqliteDatabase database;
        try
        {
            database = await SqliteDatabase.OpenAsync(options.DatabasePath, databaseKey, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            keys.Dispose();
            throw;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(databaseKey);
        }

        return new MurmurClient(keys, database, options, links, time ?? TimeProvider.System, loggerFactory ?? NullLoggerFactory.Instance);
    }

    /// <summary>Restores state from disk, connects to signaling and starts looking for contacts (architecture §21).</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _signaling.Start();
        await _connections.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<Message> SendMessageAsync(ContactId contactId, string body, CancellationToken cancellationToken = default) =>
        _sendMessage.ExecuteAsync(contactId, body, cancellationToken);

    public Task<bool> RetryMessageAsync(ConversationId conversationId, MessageId messageId, CancellationToken cancellationToken = default) =>
        _retryFailed.ExecuteAsync(conversationId, messageId, cancellationToken);

    public Task<IReadOnlyList<ConversationSummary>> ListConversationsAsync(CancellationToken cancellationToken = default) =>
        _conversations.ListSummariesAsync(cancellationToken);

    public Task<Conversation> GetConversationAsync(ContactId contactId, CancellationToken cancellationToken = default) =>
        _conversations.GetOrCreateForContactAsync(contactId, _time.GetUtcNow(), cancellationToken);

    public Task<IReadOnlyList<Message>> LoadMessagesAsync(ConversationId conversationId, int limit = 200, CancellationToken cancellationToken = default) =>
        _messages.ListRecentAsync(conversationId, limit, cancellationToken);

    public PeerConnectionState GetConnectionState(ContactId contactId) => _connections.GetState(contactId);

    /// <summary>The 60-digit code both users compare to rule out an impersonated QR code.</summary>
    public async Task<string> GetSafetyNumberAsync(ContactId contactId, CancellationToken cancellationToken = default)
    {
        var contact = await _contacts.GetAsync(contactId, cancellationToken).ConfigureAwait(false)
            ?? throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
        return SafetyNumber.Compute(_keys.IdentityPublicKey, contact.Identity.IdentityKey.Span);
    }

    /// <summary>Optional name shown to people who pair with this device. Stored only locally.</summary>
    public Task<string?> GetProfileNameAsync(CancellationToken cancellationToken = default) => _settings.GetAsync(ProfileNameSetting, cancellationToken);

    public Task SetProfileNameAsync(string? name, CancellationToken cancellationToken = default) =>
        _settings.SetAsync(ProfileNameSetting, InviteService.NormalizeProfileName(name), cancellationToken);

    /// <summary>Whether the user already went through the first-run explanation.</summary>
    public async Task<bool> IsOnboardedAsync(CancellationToken cancellationToken = default) =>
        await _settings.GetAsync(OnboardedSetting, cancellationToken).ConfigureAwait(false) is not null;

    public Task MarkOnboardedAsync(CancellationToken cancellationToken = default) =>
        _settings.SetAsync(OnboardedSetting, "1", cancellationToken);

    public async Task<CreatedInvite> CreateInviteAsync(CancellationToken cancellationToken = default) =>
        await Pairing.CreateInviteAsync(await GetProfileNameAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    public async Task<Contact> AcceptInviteAsync(string inviteText, CancellationToken cancellationToken = default) =>
        await Pairing.AcceptInviteAsync(inviteText, await GetProfileNameAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await Pairing.DisposeAsync().ConfigureAwait(false);
        await _connections.DisposeAsync().ConfigureAwait(false);
        await _signaling.DisposeAsync().ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
        _keys.Dispose();
    }
}
