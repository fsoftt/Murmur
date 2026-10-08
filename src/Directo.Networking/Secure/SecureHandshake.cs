using Directo.Domain.Model;
using Directo.Networking.Links;
using Directo.Protocol;
using Directo.Protocol.Frames;
using Directo.Security;
using Directo.Security.Identity;
using Directo.Security.Noise;

namespace Directo.Networking.Secure;

/// <summary>
/// Runs the Noise handshakes over a peer link (ADR-005):
/// <list type="bullet">
/// <item>Contact sessions use Noise_KK: both static keys are known from pairing, every connection
/// uses fresh ephemeral keys, so each session has forward secrecy.</item>
/// <item>Pairing uses Noise_IK: the scanner knows the inviter's static key from the QR code and
/// sends its own signed identity card plus the single-use token inside the encrypted payload.</item>
/// </list>
/// </summary>
public static class SecureHandshake
{
    private static readonly byte[] ContactPrologue = "Directo/v1/contact"u8.ToArray();
    private static readonly byte[] PairingPrologue = "Directo/v1/pairing"u8.ToArray();

    public static async Task<SecureSession> EstablishContactSessionAsync(
        IPeerLink link,
        bool initiator,
        LocalIdentityKeys keys,
        PublicKey remoteStaticKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(remoteStaticKey);
        using var handshake = keys.CreateHandshake(HandshakePattern.KK, initiator, ContactPrologue, remoteStaticKey.Span);
        var local = HandshakePayload.ForCurrentVersion();
        HandshakePayload remote;
        try
        {
            if (initiator)
            {
                await SendAsync(link, handshake.WriteMessage(HandshakePayloadCodec.Encode(local)), cancellationToken).ConfigureAwait(false);
                remote = HandshakePayloadCodec.Decode(handshake.ReadMessage(await ReceiveAsync(link, cancellationToken).ConfigureAwait(false)));
            }
            else
            {
                remote = HandshakePayloadCodec.Decode(handshake.ReadMessage(await ReceiveAsync(link, cancellationToken).ConfigureAwait(false)));
                await SendAsync(link, handshake.WriteMessage(HandshakePayloadCodec.Encode(local)), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (CryptoException ex)
        {
            throw new DirectoException(DirectoErrorCode.IdentityVerificationFailed, "Peer failed to prove its identity.", ex);
        }
        catch (ProtocolException ex)
        {
            throw new DirectoException(DirectoErrorCode.PeerConnectionFailed, "Peer sent an invalid handshake.", ex);
        }

        var version = Negotiate(local, remote);
        return new SecureSession(link, handshake.Split(), remoteStaticKey, version, handshake.HandshakeHash);
    }

    /// <summary>Scanner side of pairing. Returns the inviter's profile name from the encrypted response, if any.</summary>
    public static async Task<string?> InitiatePairingAsync(
        IPeerLink link,
        LocalIdentityKeys keys,
        VerifiedInvite invite,
        string? profileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(invite);
        using var handshake = keys.CreateHandshake(HandshakePattern.IK, initiator: true, PairingPrologue, invite.Card.StaticKey);
        var local = HandshakePayload.ForCurrentVersion(keys.Card, invite.Token, InviteService.NormalizeProfileName(profileName));
        try
        {
            await SendAsync(link, handshake.WriteMessage(HandshakePayloadCodec.Encode(local)), cancellationToken).ConfigureAwait(false);
            var remote = HandshakePayloadCodec.Decode(handshake.ReadMessage(await ReceiveAsync(link, cancellationToken).ConfigureAwait(false)));
            Negotiate(local, remote);
            return remote.ProfileName;
        }
        catch (CryptoException ex)
        {
            throw new DirectoException(DirectoErrorCode.IdentityVerificationFailed, "Inviter failed to prove its identity.", ex);
        }
        catch (ProtocolException ex)
        {
            throw new DirectoException(DirectoErrorCode.PeerConnectionFailed, "Inviter sent an invalid handshake.", ex);
        }
    }

    /// <summary>
    /// Inviter side of pairing. <paramref name="accept"/> receives the scanner's verified card
    /// (its static key proven by the handshake) and token, and must persist the contact before
    /// returning true. Nothing is sent back unless it accepts.
    /// </summary>
    public static async Task<bool> RespondToPairingAsync(
        IPeerLink link,
        LocalIdentityKeys keys,
        string? profileName,
        Func<PairingRequest, Task<bool>> accept,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(accept);
        using var handshake = keys.CreateHandshake(HandshakePattern.IK, initiator: false, PairingPrologue);
        var local = HandshakePayload.ForCurrentVersion(profileName: InviteService.NormalizeProfileName(profileName));
        HandshakePayload remote;
        try
        {
            remote = HandshakePayloadCodec.Decode(handshake.ReadMessage(await ReceiveAsync(link, cancellationToken).ConfigureAwait(false)));
        }
        catch (Exception ex) when (ex is CryptoException or ProtocolException)
        {
            return false;
        }

        if (remote.Card is null
            || remote.PairingToken is null
            || handshake.RemoteStaticKey is not { } proven
            || !proven.AsSpan().SequenceEqual(remote.Card.StaticKey)
            || !IdentityCardVerifier.IsValid(remote.Card))
        {
            return false;
        }

        Negotiate(local, remote);
        var request = new PairingRequest(
            new PeerIdentity(new PublicKey(remote.Card.IdentityKey), new PublicKey(remote.Card.StaticKey)),
            remote.PairingToken,
            remote.ProfileName);
        if (!await accept(request).ConfigureAwait(false))
        {
            return false;
        }

        await SendAsync(link, handshake.WriteMessage(HandshakePayloadCodec.Encode(local)), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static int Negotiate(HandshakePayload local, HandshakePayload remote)
    {
        try
        {
            return HandshakePayload.Negotiate(local, remote);
        }
        catch (ProtocolException ex)
        {
            throw new DirectoException(DirectoErrorCode.ProtocolVersionUnsupported, "Peer uses an incompatible protocol version.", ex);
        }
    }

    private static ValueTask SendAsync(IPeerLink link, byte[] message, CancellationToken cancellationToken) =>
        link.SendAsync(message, cancellationToken);

    private static async Task<byte[]> ReceiveAsync(IPeerLink link, CancellationToken cancellationToken) =>
        await link.ReceiveAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new DirectoException(DirectoErrorCode.PeerConnectionFailed, "Link closed during handshake.");
}

/// <summary>What a pairing scanner proved and asked for.</summary>
public sealed record PairingRequest(PeerIdentity Identity, byte[] Token, string? ProfileName);
