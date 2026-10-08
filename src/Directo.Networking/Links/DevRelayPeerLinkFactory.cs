using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using Directo.Networking.Signaling;

namespace Directo.Networking.Links;

/// <summary>
/// DEVELOPMENT ONLY. Tunnels a peer link through the signaling relay so the app can be exercised
/// on real devices before the WebRTC transport exists (ADR-006). Everything it carries is already
/// Noise-encrypted end to end, but it routes traffic through the server, which the architecture
/// deliberately avoids in production (no TURN). Release builds must not register it.
/// </summary>
public sealed class DevRelayPeerLinkFactory : IPeerLinkFactory
{
    internal const int HeaderSize = 1 + LinkIdSize;
    internal const int MaxChunk = 12 * 1024;
    private const int LinkIdSize = 8;

    public async Task<IPeerLink> ConnectAsync(PeerLinkRequest request, ISignalingChannel signaling, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signaling);
        var link = new DevRelayPeerLink(request.Topic, signaling);
        try
        {
            if (request.IsInitiator)
            {
                await link.OpenAsInitiatorAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await link.AcceptAsResponderAsync(cancellationToken).ConfigureAwait(false);
            }

            return link;
        }
        catch
        {
            await link.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal enum FrameType : byte
    {
        Open = 1,
        Accept = 2,
        DataFinal = 3,
        DataMore = 4,
        Close = 5,
    }

    private sealed class DevRelayPeerLink : IPeerLink
    {
        private const int MaxReassembled = IPeerLink.MaxMessageSize;

        private readonly string _topic;
        private readonly ISignalingChannel _signaling;
        private readonly Channel<byte[]> _inbox = Channel.CreateBounded<byte[]>(256);
        private readonly ConcurrentQueue<ulong> _offers = new();
        private readonly TaskCompletionSource<ulong> _accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly SemaphoreSlim _offerArrived = new(0);
        private readonly List<byte> _partial = [];
        private ulong _linkId;
        private int _closed;

        public DevRelayPeerLink(string topic, ISignalingChannel signaling)
        {
            _topic = topic;
            _signaling = signaling;
            _signaling.RelayReceived += OnRelay;
        }

        public async Task OpenAsInitiatorAsync(CancellationToken cancellationToken)
        {
            _linkId = BinaryPrimitives.ReadUInt64BigEndian(RandomNumberGenerator.GetBytes(LinkIdSize));

            // The responder may not be listening yet: repeat the offer until it is accepted.
            while (!_accepted.Task.IsCompleted)
            {
                await SendFrameAsync(FrameType.Open, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
                await Task.WhenAny(_accepted.Task, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public async Task AcceptAsResponderAsync(CancellationToken cancellationToken)
        {
            ulong offer;
            while (!_offers.TryDequeue(out offer))
            {
                await _offerArrived.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            _linkId = offer;
            await SendFrameAsync(FrameType.Accept, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
        {
            if (message.Length > IPeerLink.MaxMessageSize)
            {
                throw new ArgumentException("Message too large for the link.", nameof(message));
            }

            if (Volatile.Read(ref _closed) == 1)
            {
                throw new IOException("Link closed.");
            }

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var offset = 0;
                do
                {
                    var length = Math.Min(MaxChunk, message.Length - offset);
                    var last = offset + length == message.Length;
                    await SendFrameAsync(last ? FrameType.DataFinal : FrameType.DataMore, message.Slice(offset, length), cancellationToken).ConfigureAwait(false);
                    offset += length;
                }
                while (offset < message.Length);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _inbox.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1)
            {
                return;
            }

            _signaling.RelayReceived -= OnRelay;
            _inbox.Writer.TryComplete();
            if (_linkId != 0)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    await SendFrameAsync(FrameType.Close, ReadOnlyMemory<byte>.Empty, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _sendLock.Dispose();
            _offerArrived.Dispose();
        }

        private async Task SendFrameAsync(FrameType type, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var frame = new byte[HeaderSize + payload.Length];
            frame[0] = (byte)type;
            BinaryPrimitives.WriteUInt64BigEndian(frame.AsSpan(1), _linkId);
            payload.CopyTo(frame.AsMemory(HeaderSize));
            if (!await _signaling.TryRelayAsync(_topic, frame, cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("Signaling is not connected.");
            }
        }

        private void OnRelay(object? sender, RelayReceived relay)
        {
            if (relay.Topic != _topic || relay.Data.Length < HeaderSize || Volatile.Read(ref _closed) == 1)
            {
                return;
            }

            var type = (FrameType)relay.Data[0];
            var id = BinaryPrimitives.ReadUInt64BigEndian(relay.Data.AsSpan(1));
            switch (type)
            {
                case FrameType.Open when _linkId == 0:
                    _offers.Enqueue(id);
                    _offerArrived.Release();
                    return;
                case FrameType.Accept when id == _linkId:
                    _accepted.TrySetResult(id);
                    return;
            }

            if (_linkId == 0 || id != _linkId)
            {
                return;
            }

            switch (type)
            {
                case FrameType.DataMore or FrameType.DataFinal:
                    lock (_partial)
                    {
                        if (_partial.Count + relay.Data.Length - HeaderSize > MaxReassembled)
                        {
                            _inbox.Writer.TryComplete();
                            return;
                        }

                        _partial.AddRange(relay.Data.AsSpan(HeaderSize));
                        if (type == FrameType.DataFinal)
                        {
                            if (!_inbox.Writer.TryWrite([.. _partial]))
                            {
                                _inbox.Writer.TryComplete();
                            }

                            _partial.Clear();
                        }
                    }

                    break;
                case FrameType.Close:
                    _inbox.Writer.TryComplete();
                    break;
            }
        }
    }
}
