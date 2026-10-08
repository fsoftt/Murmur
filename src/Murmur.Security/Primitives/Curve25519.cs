using System.Security.Cryptography;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace Murmur.Security.Primitives;

/// <summary>Thin wrappers over audited BouncyCastle implementations of X25519 and Ed25519.</summary>
public static class Curve25519
{
    public const int KeySize = 32;
    public const int SignatureSize = 64;

    public static byte[] NewPrivateKey() => RandomNumberGenerator.GetBytes(KeySize);

    public static byte[] X25519PublicKey(ReadOnlySpan<byte> privateKey)
    {
        RequireKey(privateKey);
        var publicKey = new byte[KeySize];
        X25519.ScalarMultBase(privateKey.ToArray(), 0, publicKey, 0);
        return publicKey;
    }

    /// <summary>Computes X25519(privateKey, publicKey). Rejects low-order points (all-zero output).</summary>
    public static byte[] X25519Agreement(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> publicKey)
    {
        RequireKey(privateKey);
        RequireKey(publicKey);
        var shared = new byte[KeySize];
        var privateCopy = privateKey.ToArray();
        try
        {
            if (!X25519.CalculateAgreement(privateCopy, 0, publicKey.ToArray(), 0, shared, 0))
            {
                throw new CryptoException("Invalid public key (low-order point).");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateCopy);
        }

        return shared;
    }

    public static byte[] Ed25519PublicKey(ReadOnlySpan<byte> seed)
    {
        RequireKey(seed);
        var publicKey = new byte[KeySize];
        Ed25519.GeneratePublicKey(seed.ToArray(), 0, publicKey, 0);
        return publicKey;
    }

    public static byte[] Ed25519Sign(ReadOnlySpan<byte> seed, ReadOnlySpan<byte> message)
    {
        RequireKey(seed);
        var signature = new byte[SignatureSize];
        var seedCopy = seed.ToArray();
        try
        {
            var data = message.ToArray();
            Ed25519.Sign(seedCopy, 0, data, 0, data.Length, signature, 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seedCopy);
        }

        return signature;
    }

    public static bool Ed25519Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != KeySize || signature.Length != SignatureSize)
        {
            return false;
        }

        var data = message.ToArray();
        try
        {
            return Ed25519.Verify(signature.ToArray(), 0, publicKey.ToArray(), 0, data, 0, data.Length);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void RequireKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"Key must be {KeySize} bytes.");
        }
    }
}
