namespace Directo.Security.Noise;

/// <summary>
/// Post-handshake transport: one cipher per direction. Messages must be delivered reliably
/// and in order (nonces are implicit counters), which the peer transport guarantees.
/// </summary>
public sealed class NoiseTransport(CipherState send, CipherState receive) : IDisposable
{
    public const int MaxPlaintextLength = HandshakeState.MaxMessageLength - Primitives.ChaChaPoly.TagSize;

    private readonly Lock _sendLock = new();
    private readonly Lock _receiveLock = new();

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length > MaxPlaintextLength)
        {
            throw new ArgumentException("Plaintext too long for a single Noise message.", nameof(plaintext));
        }

        lock (_sendLock)
        {
            return send.EncryptWithAd([], plaintext);
        }
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length > HandshakeState.MaxMessageLength)
        {
            throw new CryptoException("Transport message too long.");
        }

        lock (_receiveLock)
        {
            return receive.DecryptWithAd([], ciphertext);
        }
    }

    public void Dispose()
    {
        send.Dispose();
        receive.Dispose();
    }
}
