using System.Security.Cryptography;
using System.Text;

namespace Directo.Security.Identity;

/// <summary>
/// Human-comparable code proving both devices see the same identity keys. Follows the design of
/// Signal's numeric fingerprint: each party's key is hashed 5200 times with SHA-512 to make
/// collision grinding expensive, rendered as 30 digits, and the two halves are sorted so both
/// sides display the same 60 digits.
/// </summary>
public static class SafetyNumber
{
    private const int Iterations = 5200;
    private const int DigitsPerChunk = 5;
    private const int ChunksPerParty = 6;
    private static readonly byte[] Version = [0, 1];
    private static readonly byte[] StableIdentifier = "Directo"u8.ToArray();

    /// <summary>Returns 60 digits grouped in blocks of five, e.g. "12345 67890 ...".</summary>
    public static string Compute(ReadOnlySpan<byte> localIdentityKey, ReadOnlySpan<byte> remoteIdentityKey)
    {
        var local = PartyDigits(localIdentityKey);
        var remote = PartyDigits(remoteIdentityKey);
        var ordered = string.CompareOrdinal(local, remote) <= 0 ? local + remote : remote + local;
        var builder = new StringBuilder(ordered.Length + ordered.Length / DigitsPerChunk);
        for (var i = 0; i < ordered.Length; i += DigitsPerChunk)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(ordered, i, DigitsPerChunk);
        }

        return builder.ToString();
    }

    private static string PartyDigits(ReadOnlySpan<byte> identityKey)
    {
        byte[] key = identityKey.ToArray();
        byte[] hash = SHA512.HashData([.. Version, .. key, .. StableIdentifier]);
        for (var i = 0; i < Iterations; i++)
        {
            hash = SHA512.HashData([.. hash, .. key]);
        }

        var builder = new StringBuilder(DigitsPerChunk * ChunksPerParty);
        for (var chunk = 0; chunk < ChunksPerParty; chunk++)
        {
            ulong value = 0;
            for (var b = 0; b < 5; b++)
            {
                value = (value << 8) | hash[chunk * 5 + b];
            }

            builder.Append((value % 100_000).ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
