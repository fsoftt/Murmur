using System.Buffers.Binary;
using System.Security.Cryptography;
using Murmur.Protocol.Signaling;

namespace Murmur.Security.Identity;

/// <summary>
/// Derives the opaque topics devices subscribe to on the signaling server. Contact topics are
/// derived from the X25519 secret shared by the two devices and rotate every UTC day, so the
/// server never sees stable identities and cannot link a pair across days by topic alone.
/// </summary>
public static class Rendezvous
{
    public static readonly TimeSpan EpochLength = TimeSpan.FromDays(1);

    /// <summary>Devices subscribe to neighbouring epochs when this close to a boundary, tolerating clock skew.</summary>
    public static readonly TimeSpan BoundaryOverlap = TimeSpan.FromHours(1);

    private static readonly byte[] ContactSalt = "Murmur/v1/rendezvous/contact"u8.ToArray();
    private static readonly byte[] PairingSalt = "Murmur/v1/rendezvous/pairing"u8.ToArray();

    public static long EpochOf(DateTimeOffset time) => time.ToUnixTimeSeconds() / (long)EpochLength.TotalSeconds;

    /// <summary>Epochs to be subscribed at <paramref name="now"/>: the current one plus a neighbour near boundaries.</summary>
    public static IReadOnlyList<long> ActiveEpochs(DateTimeOffset now)
    {
        var current = EpochOf(now);
        var epochs = new List<long> { current };
        if (EpochOf(now - BoundaryOverlap) != current)
        {
            epochs.Insert(0, current - 1);
        }

        if (EpochOf(now + BoundaryOverlap) != current)
        {
            epochs.Add(current + 1);
        }

        return epochs;
    }

    public static string ContactTopic(LocalIdentityKeys local, ReadOnlySpan<byte> remoteStaticKey, long epoch)
    {
        ArgumentNullException.ThrowIfNull(local);
        var shared = local.StaticAgreement(remoteStaticKey);
        try
        {
            Span<byte> info = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(info, epoch);
            return Derive(shared, ContactSalt, info);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
        }
    }

    public static string PairingTopic(ReadOnlySpan<byte> pairingToken) => Derive(pairingToken, PairingSalt, []);

    private static string Derive(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> info)
    {
        Span<byte> topic = stackalloc byte[RendezvousTopicFormat.TopicBytes];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, topic, salt, info);
        return RendezvousTopicFormat.Encode(topic);
    }
}
