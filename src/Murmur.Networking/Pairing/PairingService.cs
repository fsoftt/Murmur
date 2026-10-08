using System.Security.Cryptography;
using Murmur.Domain.Common;
using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Networking.Links;
using Murmur.Networking.Secure;
using Murmur.Networking.Signaling;
using Murmur.Protocol;
using Murmur.Security.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Murmur.Networking.Pairing;

/// <summary>
/// QR pairing (architecture §5). The inviter shows a signed, short-lived, single-use invite; the
/// scanner verifies it offline and both devices then meet on a topic derived from the invite
/// token to run a Noise_IK handshake that proves both identities and exchanges the scanner's card.
/// Both sides store the contact only after a successful handshake.
/// </summary>
public sealed partial class PairingService : IAsyncDisposable
{
    /// <summary>How long a scanner waits for the inviter to be reachable.</summary>
    public static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(2);

    private readonly LocalIdentityKeys _keys;
    private readonly IContactRepository _contacts;
    private readonly IConversationRepository _conversations;
    private readonly IPendingInviteRepository _invites;
    private readonly ISignalingChannel _signaling;
    private readonly IPeerLinkFactory _links;
    private readonly ChatEvents _events;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private readonly List<Task> _listeners = [];

    public PairingService(
        LocalIdentityKeys keys,
        IContactRepository contacts,
        IConversationRepository conversations,
        IPendingInviteRepository invites,
        ISignalingChannel signaling,
        IPeerLinkFactory links,
        ChatEvents events,
        TimeProvider? time = null,
        ILogger<PairingService>? logger = null)
    {
        _keys = keys;
        _contacts = contacts;
        _conversations = conversations;
        _invites = invites;
        _signaling = signaling;
        _links = links;
        _events = events;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PairingService>.Instance;
    }

    /// <summary>Raised on the inviter's side when someone completed pairing with one of its invites.</summary>
    public event EventHandler<Contact>? ContactPaired;

    /// <summary>Creates an invite for a QR code and listens for the scanner until it is used or expires.</summary>
    public async Task<CreatedInvite> CreateInviteAsync(string? profileName, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await _invites.PurgeExpiredAsync(now, cancellationToken).ConfigureAwait(false);
        var invite = InviteService.Create(_keys, now, profileName);
        await _invites.AddAsync(new PendingInvite(invite.Token, invite.ExpiresAt, null), cancellationToken).ConfigureAwait(false);

        var listener = Task.Run(() => ListenAsync(invite, profileName, _lifetime.Token), CancellationToken.None);
        lock (_gate)
        {
            _listeners.RemoveAll(t => t.IsCompleted);
            _listeners.Add(listener);
        }

        return invite;
    }

    /// <summary>Scanner side: verifies the invite, meets the inviter and completes the handshake.</summary>
    /// <exception cref="MurmurException">Invalid/expired invite, inviter not reachable, or handshake failure.</exception>
    public async Task<Contact> AcceptInviteAsync(string inviteText, string? profileName, CancellationToken cancellationToken = default)
    {
        VerifiedInvite invite;
        try
        {
            invite = InviteService.Verify(inviteText, _time.GetUtcNow());
        }
        catch (ProtocolException ex)
        {
            throw new MurmurException(
                ex.Code == ProtocolErrorCode.Expired ? MurmurErrorCode.InviteExpired : MurmurErrorCode.InviteInvalid,
                "The invite cannot be used.",
                ex);
        }

        if (invite.Card.IdentityKey.AsSpan().SequenceEqual(_keys.IdentityPublicKey))
        {
            throw new MurmurException(MurmurErrorCode.InviteInvalid, "This is your own invite.");
        }

        var topic = Rendezvous.PairingTopic(invite.Token);
        var group = $"pairing-out:{topic}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(ScanTimeout);
        _signaling.SetSubscriptions(group, [topic]);
        try
        {
            string? inviterName;
            try
            {
                await PresenceWaiter.WaitForPeerAsync(_signaling, topic, timeout.Token).ConfigureAwait(false);
                await using var link = await _links.ConnectAsync(new PeerLinkRequest(topic, IsInitiator: true), _signaling, timeout.Token).ConfigureAwait(false);
                inviterName = await SecureHandshake.InitiatePairingAsync(link, _keys, invite, profileName, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new MurmurException(MurmurErrorCode.PeerUnavailable, "The inviter could not be reached.");
            }

            var identity = new PeerIdentity(new PublicKey(invite.Card.IdentityKey), new PublicKey(invite.Card.StaticKey));
            return await StoreContactAsync(identity, invite.ProfileName ?? inviterName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _signaling.SetSubscriptions(group, []);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        Task[] listeners;
        lock (_gate)
        {
            listeners = [.. _listeners];
        }

        await Task.WhenAll(listeners).ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private async Task ListenAsync(CreatedInvite invite, string? profileName, CancellationToken cancellationToken)
    {
        var topic = Rendezvous.PairingTopic(invite.Token);
        var group = $"pairing-in:{topic}";
        using var untilExpiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        untilExpiry.CancelAfter(Max(invite.ExpiresAt - _time.GetUtcNow(), TimeSpan.Zero));
        _signaling.SetSubscriptions(group, [topic]);
        try
        {
            // Retry after failed attempts (e.g. someone who only saw the QR from afar) until the
            // invite is used by a valid scanner or expires.
            while (!untilExpiry.IsCancellationRequested)
            {
                await PresenceWaiter.WaitForPeerAsync(_signaling, topic, untilExpiry.Token).ConfigureAwait(false);
                Contact? paired = null;
                try
                {
                    using var attempt = CancellationTokenSource.CreateLinkedTokenSource(untilExpiry.Token);
                    attempt.CancelAfter(TimeSpan.FromSeconds(45));
                    await using var link = await _links.ConnectAsync(new PeerLinkRequest(topic, IsInitiator: false), _signaling, attempt.Token).ConfigureAwait(false);
                    await SecureHandshake.RespondToPairingAsync(
                        link,
                        _keys,
                        profileName,
                        async request => (paired = await TryAcceptAsync(invite, request, attempt.Token).ConfigureAwait(false)) is not null,
                        attempt.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is MurmurException or IOException || (ex is OperationCanceledException && !untilExpiry.IsCancellationRequested))
                {
                    LogPairingAttemptFailed(_logger, ex.GetType().Name);
                }

                if (paired is not null)
                {
                    ContactPaired?.Invoke(this, paired);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), _time, untilExpiry.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expired or shutting down.
        }
        finally
        {
            _signaling.SetSubscriptions(group, []);
        }
    }

    private async Task<Contact?> TryAcceptAsync(CreatedInvite invite, PairingRequest request, CancellationToken cancellationToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(request.Token, invite.Token))
        {
            return null;
        }

        var pending = await _invites.FindAsync(invite.Token, cancellationToken).ConfigureAwait(false);
        if (pending is null
            || pending.ExpiresAt + InviteService.ClockSkew < _time.GetUtcNow()
            || (pending.ConsumedBy is not null && pending.ConsumedBy != request.Identity.IdentityKey)
            || request.Identity.IdentityKey.Span.SequenceEqual(_keys.IdentityPublicKey))
        {
            return null;
        }

        var contact = await StoreContactAsync(request.Identity, request.ProfileName, cancellationToken).ConfigureAwait(false);
        await _invites.MarkConsumedAsync(invite.Token, request.Identity.IdentityKey, cancellationToken).ConfigureAwait(false);
        return contact;
    }

    /// <summary>Adds the contact, or returns the existing one when re-pairing the same identity.</summary>
    private async Task<Contact> StoreContactAsync(PeerIdentity identity, string? profileName, CancellationToken cancellationToken)
    {
        var existing = await _contacts.FindByIdentityKeyAsync(identity.IdentityKey, cancellationToken).ConfigureAwait(false);
        Contact contact;
        if (existing is not null)
        {
            // Same identity; a new static key means the peer reinstalled keys under the same identity.
            contact = existing.Identity.Equals(identity) ? existing : existing with { Identity = identity, Verification = VerificationState.Unverified };
            if (!ReferenceEquals(contact, existing))
            {
                await _contacts.UpdateAsync(contact, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var fallbackName = "Contacto " + Convert.ToHexString(identity.IdentityKey.Span[..2]);
            contact = new Contact(
                ContactId.New(),
                identity,
                Contact.NormalizeDisplayName(profileName, fallbackName),
                VerificationState.Unverified,
                IsBlocked: false,
                _time.GetUtcNow());
            try
            {
                await _contacts.AddAsync(contact, cancellationToken).ConfigureAwait(false);
            }
            catch (MurmurException ex) when (ex.Code == MurmurErrorCode.AlreadyPaired)
            {
                contact = (await _contacts.FindByIdentityKeyAsync(identity.IdentityKey, cancellationToken).ConfigureAwait(false))!;
            }
        }

        await _conversations.GetOrCreateForContactAsync(contact.Id, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        _events.OnContactsChanged();
        return contact;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    [LoggerMessage(Level = LogLevel.Information, Message = "Pairing attempt failed ({Reason})")]
    private static partial void LogPairingAttemptFailed(ILogger logger, string reason);
}
