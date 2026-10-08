using System.Security.Cryptography;
using Directo.Protocol.Identity;
using Directo.Security.Noise;
using Directo.Security.Primitives;

namespace Directo.Security.Identity;

/// <summary>
/// The device's long-term secrets: an Ed25519 identity seed (signing) and an X25519 static key
/// (Noise handshakes). They are created locally, persisted only through the platform secret store
/// and never leave the device.
/// </summary>
public sealed class LocalIdentityKeys : IDisposable
{
    private const byte SerializationVersion = 1;
    private const int SerializedLength = 1 + Curve25519.KeySize * 2;

    private readonly byte[] _identitySeed;
    private readonly byte[] _staticPrivate;

    private LocalIdentityKeys(byte[] identitySeed, byte[] staticPrivate)
    {
        _identitySeed = identitySeed;
        _staticPrivate = staticPrivate;
        IdentityPublicKey = Curve25519.Ed25519PublicKey(identitySeed);
        StaticPublicKey = Curve25519.X25519PublicKey(staticPrivate);
        Card = new IdentityCard(
            IdentityPublicKey,
            StaticPublicKey,
            Curve25519.Ed25519Sign(identitySeed, IdentityCard.ToBeSigned(IdentityPublicKey, StaticPublicKey)));
    }

    public byte[] IdentityPublicKey { get; }

    public byte[] StaticPublicKey { get; }

    /// <summary>Signed public card shared with contacts.</summary>
    public IdentityCard Card { get; }

    public static LocalIdentityKeys Generate() => new(Curve25519.NewPrivateKey(), Curve25519.NewPrivateKey());

    public static LocalIdentityKeys Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length != SerializedLength || data[0] != SerializationVersion)
        {
            throw new CryptoException("Unrecognized identity key format.");
        }

        return new LocalIdentityKeys(data.Slice(1, Curve25519.KeySize).ToArray(), data.Slice(1 + Curve25519.KeySize).ToArray());
    }

    /// <summary>Serialized secret material for the platform secret store. Caller must wipe the result after use.</summary>
    public byte[] Serialize() => [SerializationVersion, .. _identitySeed, .. _staticPrivate];

    public byte[] Sign(ReadOnlySpan<byte> message) => Curve25519.Ed25519Sign(_identitySeed, message);

    /// <summary>X25519 with the static key; used to derive per-contact rendezvous topics.</summary>
    public byte[] StaticAgreement(ReadOnlySpan<byte> remoteStaticPublic) => Curve25519.X25519Agreement(_staticPrivate, remoteStaticPublic);

    /// <summary>Starts a Noise handshake with the static key, without exposing it to callers.</summary>
    public HandshakeState CreateHandshake(HandshakePattern pattern, bool initiator, ReadOnlySpan<byte> prologue, ReadOnlySpan<byte> remoteStaticKey = default) =>
        new(pattern, initiator, prologue, _staticPrivate, remoteStaticKey);

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_identitySeed);
        CryptographicOperations.ZeroMemory(_staticPrivate);
    }
}
