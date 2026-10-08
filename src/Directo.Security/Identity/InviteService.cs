using System.Security.Cryptography;
using Directo.Protocol;
using Directo.Protocol.Identity;
using Directo.Security.Primitives;

namespace Directo.Security.Identity;

/// <summary>An invite whose signatures and expiration were checked.</summary>
public sealed record VerifiedInvite(IdentityCard Card, byte[] Token, DateTimeOffset ExpiresAt, string? ProfileName);

/// <summary>A freshly created invite and the token the inviter must remember until it is used or expires.</summary>
public sealed record CreatedInvite(string Text, byte[] Token, DateTimeOffset ExpiresAt);

public static class InviteService
{
    /// <summary>Default validity of a QR invite; short on purpose.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Upper bound accepted from others, so stolen invites cannot be long-lived.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(24);

    /// <summary>Tolerated clock difference between devices.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public static CreatedInvite Create(LocalIdentityKeys keys, DateTimeOffset now, string? profileName = null, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var validFor = lifetime ?? DefaultLifetime;
        if (validFor <= TimeSpan.Zero || validFor > MaxLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        var token = RandomNumberGenerator.GetBytes(ProtocolConstants.PairingTokenSize);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds((now + validFor).ToUnixTimeSeconds());
        var body = InviteCodec.EncodeBody(new InviteBody(keys.Card, token, expiresAt, NormalizeProfileName(profileName)));
        var signature = keys.Sign(SignedInvite.ToBeSigned(body));
        return new CreatedInvite(InviteCodec.Encode(body, signature), token, expiresAt);
    }

    /// <summary>Parses and fully verifies an invite scanned from a QR code.</summary>
    /// <exception cref="ProtocolException">Malformed, expired or wrongly signed invite.</exception>
    public static VerifiedInvite Verify(string text, DateTimeOffset now)
    {
        var invite = InviteCodec.Decode(text);
        var card = invite.Body.Card;
        if (!IdentityCardVerifier.IsValid(card) || !Curve25519.Ed25519Verify(card.IdentityKey, invite.ToBeSigned(), invite.Signature))
        {
            throw new ProtocolException(ProtocolErrorCode.InvalidSignature, "Invite signature is invalid.");
        }

        if (invite.Body.ExpiresAt + ClockSkew < now)
        {
            throw new ProtocolException(ProtocolErrorCode.Expired, "Invite has expired.");
        }

        if (invite.Body.ExpiresAt > now + MaxLifetime + ClockSkew)
        {
            throw ProtocolExceptionFactory.Malformed("Invite lifetime exceeds the allowed maximum.");
        }

        return new VerifiedInvite(card, invite.Body.Token, invite.Body.ExpiresAt, invite.Body.ProfileName);
    }

    public static string? NormalizeProfileName(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        var trimmed = profileName.Trim();
        while (System.Text.Encoding.UTF8.GetByteCount(trimmed) > ProtocolConstants.MaxProfileNameBytes)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }
}

internal static class ProtocolExceptionFactory
{
    public static ProtocolException Malformed(string message) => new(ProtocolErrorCode.Malformed, message);
}
