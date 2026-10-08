using System.Buffers.Binary;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Murmur.Security.Primitives;

/// <summary>ChaCha20-Poly1305 AEAD (RFC 8439) with the Noise nonce layout: 4 zero bytes followed by a 64-bit little-endian counter.</summary>
internal static class ChaChaPoly
{
    public const int TagSize = 16;
    private const int MacBits = TagSize * 8;

    public static byte[] Encrypt(byte[] key, ulong nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext)
    {
        var cipher = Create(forEncryption: true, key, nonce, associatedData);
        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var length = cipher.ProcessBytes(plaintext.ToArray(), 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, length);
        return output;
    }

    public static byte[] Decrypt(byte[] key, ulong nonce, ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < TagSize)
        {
            throw new CryptoException("Ciphertext too short.");
        }

        var cipher = Create(forEncryption: false, key, nonce, associatedData);
        var output = new byte[cipher.GetOutputSize(ciphertext.Length)];
        try
        {
            var length = cipher.ProcessBytes(ciphertext.ToArray(), 0, ciphertext.Length, output, 0);
            cipher.DoFinal(output, length);
        }
        catch (InvalidCipherTextException)
        {
            throw new CryptoException("Message authentication failed.");
        }

        return output;
    }

    private static ChaCha20Poly1305 Create(bool forEncryption, byte[] key, ulong nonce, ReadOnlySpan<byte> associatedData)
    {
        var nonceBytes = new byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(nonceBytes.AsSpan(4), nonce);
        var cipher = new ChaCha20Poly1305();
        cipher.Init(forEncryption, new AeadParameters(new KeyParameter(key), MacBits, nonceBytes, associatedData.ToArray()));
        return cipher;
    }
}
