using System.Security.Cryptography;
using Murmur.Security.Primitives;

namespace Murmur.Security.Noise;

/// <summary>
/// Noise HandshakeState (spec §5.3) restricted to the KK and IK patterns over 25519/ChaChaPoly/SHA256.
/// This is a direct transcription of the public specification on top of audited primitives; it is
/// validated against an independent implementation in the test suite.
/// </summary>
public sealed class HandshakeState : IDisposable
{
    private const int DhLength = Curve25519.KeySize;
    public const int MaxMessageLength = 65535;

    private readonly HandshakePattern _pattern;
    private readonly bool _initiator;
    private readonly SymmetricState _symmetric;
    private readonly byte[] _localStaticPrivate;
    private readonly byte[] _localStaticPublic;
    private byte[]? _localEphemeralPrivate;
    private byte[]? _remoteEphemeral;
    private byte[]? _remoteStatic;
    private int _messageIndex;
    private bool _failed;

    /// <param name="localStaticPrivate">Own X25519 static private key.</param>
    /// <param name="remoteStatic">Peer static public key, required when the pattern has the matching pre-message.</param>
    /// <param name="fixedEphemeral">Only for deterministic test vectors. Never set in production.</param>
    public HandshakeState(
        HandshakePattern pattern,
        bool initiator,
        ReadOnlySpan<byte> prologue,
        ReadOnlySpan<byte> localStaticPrivate,
        ReadOnlySpan<byte> remoteStatic = default,
        byte[]? fixedEphemeral = null)
    {
        _pattern = pattern;
        _initiator = initiator;
        _localStaticPrivate = localStaticPrivate.ToArray();
        _localStaticPublic = Curve25519.X25519PublicKey(_localStaticPrivate);
        _localEphemeralPrivate = fixedEphemeral?.ToArray();
        if (!remoteStatic.IsEmpty)
        {
            _remoteStatic = remoteStatic.ToArray();
        }

        _symmetric = new SymmetricState(pattern.ProtocolName);
        _symmetric.MixHash(prologue);

        if (pattern.InitiatorPreMessage)
        {
            _symmetric.MixHash(initiator ? _localStaticPublic : RequireRemoteStatic());
        }

        if (pattern.ResponderPreMessage)
        {
            _symmetric.MixHash(initiator ? RequireRemoteStatic() : _localStaticPublic);
        }
    }

    public bool IsCompleted => _messageIndex >= _pattern.Messages.Length;

    /// <summary>Peer static public key; known after the handshake (or from the start for KK).</summary>
    public byte[]? RemoteStaticKey => _remoteStatic?.ToArray();

    /// <summary>Unique, both-sides-agreed transcript hash. Usable for channel binding.</summary>
    public byte[] HandshakeHash => _symmetric.HandshakeHash;

    public byte[] WriteMessage(ReadOnlySpan<byte> payload)
    {
        var tokens = NextTokens(writing: true);
        var buffer = new List<byte>(DhLength * 2 + payload.Length + 32);
        try
        {
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case Token.E:
                        _localEphemeralPrivate ??= Curve25519.NewPrivateKey();
                        var ephemeralPublic = Curve25519.X25519PublicKey(_localEphemeralPrivate);
                        buffer.AddRange(ephemeralPublic);
                        _symmetric.MixHash(ephemeralPublic);
                        break;
                    case Token.S:
                        buffer.AddRange(_symmetric.EncryptAndHash(_localStaticPublic));
                        break;
                    default:
                        MixDh(token);
                        break;
                }
            }

            buffer.AddRange(_symmetric.EncryptAndHash(payload));
        }
        catch
        {
            _failed = true;
            throw;
        }

        _messageIndex++;
        if (buffer.Count > MaxMessageLength)
        {
            _failed = true;
            throw new CryptoException("Handshake message too long.");
        }

        return [.. buffer];
    }

    public byte[] ReadMessage(ReadOnlySpan<byte> message)
    {
        if (message.Length > MaxMessageLength)
        {
            _failed = true;
            throw new CryptoException("Handshake message too long.");
        }

        var tokens = NextTokens(writing: false);
        var offset = 0;
        try
        {
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case Token.E:
                        _remoteEphemeral = Take(message, ref offset, DhLength);
                        _symmetric.MixHash(_remoteEphemeral);
                        break;
                    case Token.S:
                        var length = _symmetric.HasKey ? DhLength + ChaChaPoly.TagSize : DhLength;
                        _remoteStatic = _symmetric.DecryptAndHash(Take(message, ref offset, length));
                        break;
                    default:
                        MixDh(token);
                        break;
                }
            }

            var payload = _symmetric.DecryptAndHash(message[offset..]);
            _messageIndex++;
            return payload;
        }
        catch
        {
            _failed = true;
            throw;
        }
    }

    /// <summary>Derives the transport ciphers once all handshake messages were processed.</summary>
    public NoiseTransport Split()
    {
        if (!IsCompleted || _failed)
        {
            throw new InvalidOperationException("Handshake is not complete.");
        }

        var (first, second) = _symmetric.Split();
        return _initiator ? new NoiseTransport(send: first, receive: second) : new NoiseTransport(send: second, receive: first);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_localStaticPrivate);
        if (_localEphemeralPrivate is not null)
        {
            CryptographicOperations.ZeroMemory(_localEphemeralPrivate);
        }

        _symmetric.Dispose();
    }

    private Token[] NextTokens(bool writing)
    {
        if (_failed)
        {
            throw new InvalidOperationException("Handshake already failed.");
        }

        if (IsCompleted)
        {
            throw new InvalidOperationException("Handshake already completed.");
        }

        var initiatorTurn = _messageIndex % 2 == 0;
        if (initiatorTurn != (_initiator == writing))
        {
            throw new InvalidOperationException("Out-of-turn handshake message.");
        }

        return _pattern.Messages[_messageIndex];
    }

    private void MixDh(Token token)
    {
        // Token names are written from the initiator's perspective: the first letter is the
        // initiator's key, the second the responder's (spec §7.1).
        var (initiatorKey, responderKey) = token switch
        {
            Token.EE => ('e', 'e'),
            Token.ES => ('e', 's'),
            Token.SE => ('s', 'e'),
            Token.SS => ('s', 's'),
            _ => throw new InvalidOperationException($"Unexpected token {token}."),
        };
        var (local, remote) = _initiator ? (initiatorKey, responderKey) : (responderKey, initiatorKey);
        var privateKey = local == 'e' ? _localEphemeralPrivate : _localStaticPrivate;
        var publicKey = remote == 'e' ? _remoteEphemeral : _remoteStatic;
        if (privateKey is null || publicKey is null)
        {
            throw new CryptoException("Missing key material for handshake.");
        }

        _symmetric.MixKey(Curve25519.X25519Agreement(privateKey, publicKey));
    }

    private byte[] RequireRemoteStatic() =>
        _remoteStatic ?? throw new ArgumentException("Pattern requires the remote static key.");

    private static byte[] Take(ReadOnlySpan<byte> message, ref int offset, int length)
    {
        if (message.Length - offset < length)
        {
            throw new CryptoException("Handshake message truncated.");
        }

        var slice = message.Slice(offset, length).ToArray();
        offset += length;
        return slice;
    }
}
