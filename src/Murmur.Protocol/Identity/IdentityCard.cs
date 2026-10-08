using Murmur.Protocol.Serialization;

namespace Murmur.Protocol.Identity;

/// <summary>
/// Public description of a device: its long-term Ed25519 identity key, the X25519 static key
/// used for Noise handshakes, and the identity key's signature binding both together.
/// </summary>
public sealed record IdentityCard(byte[] IdentityKey, byte[] StaticKey, byte[] Signature)
{
    private static readonly byte[] SigningContext = "Murmur/v1/identity-card"u8.ToArray();

    /// <summary>Bytes covered by <see cref="Signature"/>.</summary>
    public static byte[] ToBeSigned(ReadOnlySpan<byte> identityKey, ReadOnlySpan<byte> staticKey) =>
        [.. SigningContext, .. identityKey, .. staticKey];

    public byte[] ToBeSigned() => ToBeSigned(IdentityKey, StaticKey);
}

public static class IdentityCardCodec
{
    private const uint KeyVersion = 0;
    private const uint KeyIdentity = 1;
    private const uint KeyStatic = 2;
    private const uint KeySignature = 3;
    private const int CardVersion = 1;
    private const int MaxBytes = 256;

    public static byte[] Encode(IdentityCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var writer = CborMap.CreateWriter();
        writer.WriteStartMap(4);
        writer.WriteUInt32(KeyVersion);
        writer.WriteInt32(CardVersion);
        writer.WriteUInt32(KeyIdentity);
        writer.WriteByteString(card.IdentityKey);
        writer.WriteUInt32(KeyStatic);
        writer.WriteByteString(card.StaticKey);
        writer.WriteUInt32(KeySignature);
        writer.WriteByteString(card.Signature);
        writer.WriteEndMap();
        return writer.Encode();
    }

    public static IdentityCard Decode(ReadOnlyMemory<byte> data)
    {
        var reader = CborMap.CreateReader(data, MaxBytes);
        int? version = null;
        byte[]? identity = null;
        byte[]? staticKey = null;
        byte[]? signature = null;
        CborMap.ReadMap(reader, (key, r) =>
        {
            switch (key)
            {
                case KeyVersion:
                    version = r.ReadInt32();
                    return true;
                case KeyIdentity:
                    identity = CborMap.ReadFixedBytes(r, ProtocolConstants.PublicKeySize, "identityKey");
                    return true;
                case KeyStatic:
                    staticKey = CborMap.ReadFixedBytes(r, ProtocolConstants.PublicKeySize, "staticKey");
                    return true;
                case KeySignature:
                    signature = CborMap.ReadFixedBytes(r, ProtocolConstants.SignatureSize, "signature");
                    return true;
                default:
                    return false;
            }
        });
        CborMap.EnsureFullyConsumed(reader);

        if (CborMap.Required(version, "version") != CardVersion)
        {
            throw new ProtocolException(ProtocolErrorCode.UnsupportedVersion, "Unsupported identity card version.");
        }

        return new IdentityCard(
            CborMap.Required(identity, "identityKey"),
            CborMap.Required(staticKey, "staticKey"),
            CborMap.Required(signature, "signature"));
    }
}
