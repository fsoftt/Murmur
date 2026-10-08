using System.Security.Cryptography;
using Directo.Security.Primitives;

namespace Directo.Security.Noise;

/// <summary>Noise CipherState (spec rev. 34, §5.1) for ChaChaPoly.</summary>
public sealed class CipherState : IDisposable
{
    private const ulong MaxNonce = ulong.MaxValue; // reserved by the spec
    private byte[]? _key;
    private ulong _nonce;

    internal CipherState(byte[]? key = null)
    {
        if (key is not null)
        {
            InitializeKey(key);
        }
    }

    public bool HasKey => _key is not null;

    internal void InitializeKey(byte[] key)
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
        }

        _key = key[..32];
        _nonce = 0;
    }

    public byte[] EncryptWithAd(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> plaintext)
    {
        if (_key is null)
        {
            return plaintext.ToArray();
        }

        EnsureNonceAvailable();
        var ciphertext = ChaChaPoly.Encrypt(_key, _nonce, associatedData, plaintext);
        _nonce++;
        return ciphertext;
    }

    public byte[] DecryptWithAd(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> ciphertext)
    {
        if (_key is null)
        {
            return ciphertext.ToArray();
        }

        EnsureNonceAvailable();
        var plaintext = ChaChaPoly.Decrypt(_key, _nonce, associatedData, ciphertext);
        _nonce++;
        return plaintext;
    }

    public void Dispose()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }

    private void EnsureNonceAvailable()
    {
        if (_nonce == MaxNonce)
        {
            throw new CryptoException("Nonce space exhausted; session must be re-established.");
        }
    }
}
