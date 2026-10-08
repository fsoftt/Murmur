using System.Runtime.CompilerServices;
using Directo.Domain.Model;
using Directo.Domain.Ports;
using Directo.Networking.Links;
using Directo.Protocol;
using Directo.Protocol.Frames;
using Directo.Security;
using Directo.Security.Noise;

namespace Directo.Networking.Secure;

/// <summary>
/// End-to-end encrypted session to a contact: Noise transport ciphers over a peer link.
/// Any authentication or parsing failure ends the session; the delivery layer reconnects.
/// </summary>
public sealed class SecureSession : IPeerChannel, IAsyncDisposable
{
    private readonly IPeerLink _link;
    private readonly NoiseTransport _transport;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    internal SecureSession(IPeerLink link, NoiseTransport transport, PublicKey remoteStaticKey, int protocolVersion, byte[] handshakeHash)
    {
        _link = link;
        _transport = transport;
        RemoteStaticKey = remoteStaticKey;
        ProtocolVersion = protocolVersion;
        HandshakeHash = handshakeHash;
    }

    public PublicKey RemoteStaticKey { get; }

    public int ProtocolVersion { get; }

    public byte[] HandshakeHash { get; }

    public ValueTask SendMessageAsync(Message message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendFrameAsync(new ChatMessageFrame(message.Id.Value, message.Lamport, message.CreatedAt.ToUnixTimeMilliseconds(), message.Body), cancellationToken);
    }

    public ValueTask SendAckAsync(MessageId id, CancellationToken cancellationToken) => SendFrameAsync(new AckFrame(id.Value), cancellationToken);

    public async IAsyncEnumerable<PeerEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var ciphertext = await _link.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (ciphertext is null)
            {
                yield break;
            }

            PeerFrame frame;
            try
            {
                frame = PeerFrameCodec.Decode(_transport.Decrypt(ciphertext));
            }
            catch (CryptoException ex)
            {
                throw new DirectoException(DirectoErrorCode.MessageAuthenticationFailed, "Received data failed authentication.", ex);
            }
            catch (ProtocolException ex)
            {
                throw new DirectoException(DirectoErrorCode.PeerConnectionFailed, "Peer sent a malformed frame.", ex);
            }

            switch (frame)
            {
                case ChatMessageFrame message:
                    yield return new PeerMessageReceived(
                        new MessageId(message.MessageId),
                        message.Lamport,
                        SafeTimestamp(message.SentAtUnixMs),
                        message.Body);
                    break;
                case AckFrame ack:
                    yield return new PeerAckReceived(new MessageId(ack.MessageId));
                    break;
                default:
                    // Frame types from newer protocol revisions are ignored.
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _link.DisposeAsync().ConfigureAwait(false);
        _transport.Dispose();
        _sendLock.Dispose();
    }

    private async ValueTask SendFrameAsync(PeerFrame frame, CancellationToken cancellationToken)
    {
        var plaintext = PeerFrameCodec.Encode(frame);

        // Encryption and sending must happen in the same order: nonces are implicit counters.
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _link.SendAsync(_transport.Encrypt(plaintext), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static DateTimeOffset SafeTimestamp(long unixMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(unixMs, 0, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()));
}
