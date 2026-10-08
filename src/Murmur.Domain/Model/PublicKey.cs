namespace Murmur.Domain.Model;

/// <summary>An immutable 32-byte public key (Ed25519 identity or X25519 static).</summary>
public sealed class PublicKey : IEquatable<PublicKey>
{
    public const int Size = 32;
    private readonly byte[] _bytes;

    public PublicKey(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"Public key must be {Size} bytes.", nameof(bytes));
        }

        _bytes = bytes.ToArray();
    }

    public ReadOnlySpan<byte> Span => _bytes;

    public byte[] ToArray() => (byte[])_bytes.Clone();

    public bool Equals(PublicKey? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is PublicKey other && Equals(other);

    public override int GetHashCode() => BitConverter.ToInt32(_bytes, 0);

    /// <summary>Short, non-unique prefix for diagnostics only.</summary>
    public override string ToString() => Convert.ToHexStringLower(_bytes, 0, 4) + "…";

    public static bool operator ==(PublicKey? left, PublicKey? right) => Equals(left, right);

    public static bool operator !=(PublicKey? left, PublicKey? right) => !Equals(left, right);
}

/// <summary>Cryptographic identity of a peer device: who it is (identity key) and how to handshake with it (static key).</summary>
public sealed record PeerIdentity(PublicKey IdentityKey, PublicKey StaticKey);
