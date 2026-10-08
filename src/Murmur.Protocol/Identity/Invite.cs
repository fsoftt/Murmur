using System.Buffers.Text;
using Murmur.Protocol.Serialization;

namespace Murmur.Protocol.Identity;

/// <summary>
/// Contents of a pairing QR code. It never contains secrets: only the inviter's public card,
/// a random single-use token, an expiration and an optional self-chosen profile name.
/// The whole body is signed with the inviter's identity key.
/// </summary>
public sealed record InviteBody(IdentityCard Card, byte[] Token, DateTimeOffset ExpiresAt, string? ProfileName);

/// <summary>A signed invite as transported in a QR code.</summary>
/// <param name="Body">Decoded body.</param>
/// <param name="SignedBytes">Exact encoded body bytes covered by <paramref name="Signature"/>.</param>
/// <param name="Signature">Ed25519 signature by <c>Body.Card.IdentityKey</c> over <see cref="ToBeSigned"/>.</param>
public sealed record SignedInvite(InviteBody Body, byte[] SignedBytes, byte[] Signature)
{
    private static readonly byte[] SigningContext = "Murmur/v1/invite"u8.ToArray();

    public static byte[] ToBeSigned(ReadOnlySpan<byte> encodedBody) => [.. SigningContext, .. encodedBody];

    public byte[] ToBeSigned() => ToBeSigned(SignedBytes);
}

/// <summary>
/// Text format: <c>MURMUR1:</c> followed by base64url(CBOR [bodyBytes, signature]).
/// </summary>
public static class InviteCodec
{
    public const string Prefix = "MURMUR1:";

    private const uint KeyVersion = 0;
    private const uint KeyCard = 1;
    private const uint KeyToken = 2;
    private const uint KeyExpiresAt = 3;
    private const uint KeyProfileName = 4;

    private const uint KeyBody = 0;
    private const uint KeySignature = 1;

    private const int InviteVersion = 1;

    /// <summary>Encodes the body; the caller signs <see cref="SignedInvite.ToBeSigned(ReadOnlySpan{byte})"/> of the result.</summary>
    public static byte[] EncodeBody(InviteBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Token.Length != ProtocolConstants.PairingTokenSize)
        {
            throw new ArgumentException("Invalid token size.", nameof(body));
        }

        var writer = CborMap.CreateWriter();
        writer.WriteStartMap(body.ProfileName is null ? 4 : 5);
        writer.WriteUInt32(KeyVersion);
        writer.WriteInt32(InviteVersion);
        writer.WriteUInt32(KeyCard);
        writer.WriteByteString(IdentityCardCodec.Encode(body.Card));
        writer.WriteUInt32(KeyToken);
        writer.WriteByteString(body.Token);
        writer.WriteUInt32(KeyExpiresAt);
        writer.WriteInt64(body.ExpiresAt.ToUnixTimeSeconds());
        if (body.ProfileName is not null)
        {
            writer.WriteUInt32(KeyProfileName);
            writer.WriteTextString(body.ProfileName);
        }

        writer.WriteEndMap();
        return writer.Encode();
    }

    public static string Encode(byte[] bodyBytes, byte[] signature)
    {
        var writer = CborMap.CreateWriter();
        writer.WriteStartMap(2);
        writer.WriteUInt32(KeyBody);
        writer.WriteByteString(bodyBytes);
        writer.WriteUInt32(KeySignature);
        writer.WriteByteString(signature);
        writer.WriteEndMap();
        return Prefix + Base64Url.EncodeToString(writer.Encode());
    }

    /// <summary>Parses the structure. Signature and expiration are verified by the security layer.</summary>
    public static SignedInvite Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw ProtocolException.Malformed("Not a Murmur invite.");
        }

        var encoded = text.AsSpan(Prefix.Length);
        if (Base64Url.GetMaxDecodedLength(encoded.Length) > ProtocolConstants.MaxInviteBytes)
        {
            throw new ProtocolException(ProtocolErrorCode.TooLarge, "Invite too large.");
        }

        byte[] raw;
        try
        {
            raw = Base64Url.DecodeFromChars(encoded);
        }
        catch (FormatException ex)
        {
            throw ProtocolException.Malformed("Invite is not valid base64url.", ex);
        }

        var reader = CborMap.CreateReader(raw, ProtocolConstants.MaxInviteBytes);
        byte[]? bodyBytes = null;
        byte[]? signature = null;
        CborMap.ReadMap(reader, (key, r) =>
        {
            switch (key)
            {
                case KeyBody:
                    bodyBytes = r.ReadByteString();
                    return true;
                case KeySignature:
                    signature = CborMap.ReadFixedBytes(r, ProtocolConstants.SignatureSize, "signature");
                    return true;
                default:
                    return false;
            }
        });
        CborMap.EnsureFullyConsumed(reader);

        var body = DecodeBody(CborMap.Required(bodyBytes, "body"));
        return new SignedInvite(body, bodyBytes!, CborMap.Required(signature, "signature"));
    }

    private static InviteBody DecodeBody(byte[] bodyBytes)
    {
        var reader = CborMap.CreateReader(bodyBytes, ProtocolConstants.MaxInviteBytes);
        int? version = null;
        IdentityCard? card = null;
        byte[]? token = null;
        long? expiresAt = null;
        string? profileName = null;
        CborMap.ReadMap(reader, (key, r) =>
        {
            switch (key)
            {
                case KeyVersion:
                    version = r.ReadInt32();
                    return true;
                case KeyCard:
                    card = IdentityCardCodec.Decode(r.ReadByteString());
                    return true;
                case KeyToken:
                    token = CborMap.ReadFixedBytes(r, ProtocolConstants.PairingTokenSize, "token");
                    return true;
                case KeyExpiresAt:
                    expiresAt = r.ReadInt64();
                    return true;
                case KeyProfileName:
                    profileName = CborMap.ReadBoundedText(r, ProtocolConstants.MaxProfileNameBytes, "profileName");
                    return true;
                default:
                    return false;
            }
        });
        CborMap.EnsureFullyConsumed(reader);

        if (CborMap.Required(version, "version") != InviteVersion)
        {
            throw new ProtocolException(ProtocolErrorCode.UnsupportedVersion, "Unsupported invite version.");
        }

        DateTimeOffset expiry;
        try
        {
            expiry = DateTimeOffset.FromUnixTimeSeconds(CborMap.Required(expiresAt, "expiresAt"));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw ProtocolException.Malformed("Invalid expiration.", ex);
        }

        return new InviteBody(CborMap.Required(card, "card"), CborMap.Required(token, "token"), expiry, profileName);
    }
}
