using System.Security.Cryptography;
using System.Text;

namespace Murmur.Security.Noise;

/// <summary>Noise SymmetricState (spec §5.2) with SHA-256.</summary>
internal sealed class SymmetricState : IDisposable
{
    public const int HashLength = 32;

    private readonly CipherState _cipher = new();
    private byte[] _chainingKey;
    private byte[] _hash;

    public SymmetricState(string protocolName)
    {
        var name = Encoding.ASCII.GetBytes(protocolName);
        _hash = name.Length <= HashLength ? [.. name, .. new byte[HashLength - name.Length]] : SHA256.HashData(name);
        _chainingKey = (byte[])_hash.Clone();
    }

    public byte[] HandshakeHash => (byte[])_hash.Clone();

    public bool HasKey => _cipher.HasKey;

    public void MixKey(byte[] inputKeyMaterial)
    {
        var (chainingKey, tempKey) = Hkdf2(_chainingKey, inputKeyMaterial);
        CryptographicOperations.ZeroMemory(_chainingKey);
        _chainingKey = chainingKey;
        _cipher.InitializeKey(tempKey);
        CryptographicOperations.ZeroMemory(tempKey);
        CryptographicOperations.ZeroMemory(inputKeyMaterial);
    }

    public void MixHash(ReadOnlySpan<byte> data) => _hash = SHA256.HashData([.. _hash, .. data]);

    public byte[] EncryptAndHash(ReadOnlySpan<byte> plaintext)
    {
        var ciphertext = _cipher.EncryptWithAd(_hash, plaintext);
        MixHash(ciphertext);
        return ciphertext;
    }

    public byte[] DecryptAndHash(ReadOnlySpan<byte> ciphertext)
    {
        var plaintext = _cipher.DecryptWithAd(_hash, ciphertext);
        MixHash(ciphertext);
        return plaintext;
    }

    public (CipherState First, CipherState Second) Split()
    {
        var (k1, k2) = Hkdf2(_chainingKey, []);
        var result = (new CipherState(k1), new CipherState(k2));
        CryptographicOperations.ZeroMemory(k1);
        CryptographicOperations.ZeroMemory(k2);
        return result;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_chainingKey);
        _cipher.Dispose();
    }

    /// <summary>Noise HKDF (spec §4.3) producing two outputs.</summary>
    private static (byte[] Output1, byte[] Output2) Hkdf2(byte[] chainingKey, byte[] inputKeyMaterial)
    {
        var tempKey = HMACSHA256.HashData(chainingKey, inputKeyMaterial);
        var output1 = HMACSHA256.HashData(tempKey, (byte[])[0x01]);
        var output2 = HMACSHA256.HashData(tempKey, (byte[])[.. output1, 0x02]);
        CryptographicOperations.ZeroMemory(tempKey);
        return (output1, output2);
    }
}
