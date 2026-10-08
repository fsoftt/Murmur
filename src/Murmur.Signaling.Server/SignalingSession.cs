using System.Buffers;
using System.Net.WebSockets;
using Murmur.Protocol.Signaling;

namespace Murmur.Signaling.Server;

/// <summary>
/// Protocol handling for one WebSocket. The server only learns opaque topics and opaque relay
/// blobs; it never stores anything beyond the lifetime of the connection.
/// </summary>
public sealed partial class SignalingSession(ITopicHub hub, SignalingOptions options, TimeProvider time, ILogger<SignalingSession> logger)
{
    public async Task RunAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var connection = new SignalingConnection(socket, options.MaxOutboundQueue);
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var writer = connection.RunWriterAsync(session.Token);

        // A client that stops reading (overloaded queue) or a failed write ends the session
        // even while we are waiting for its next message.
        _ = writer.ContinueWith(
            static (_, state) =>
            {
                try
                {
                    ((CancellationTokenSource)state!).Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            },
            session,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        var buffer = ArrayPool<byte>.Shared.Rent(SignalingCodec.MaxFrameBytes);
        var closeStatus = WebSocketCloseStatus.NormalClosure;
        try
        {
            if (!await HandshakeAsync(socket, connection, buffer, session.Token).ConfigureAwait(false))
            {
                closeStatus = WebSocketCloseStatus.PolicyViolation;
                return;
            }

            var bucket = new TokenBucket(options.MessagesPerSecond, options.MessageBurst, time);
            while (!connection.IsOverloaded && !writer.IsCompleted)
            {
                var (status, message) = await ReceiveAsync(socket, buffer, session.Token).ConfigureAwait(false);
                if (status is not null)
                {
                    closeStatus = status.Value;
                    return;
                }

                if (!bucket.TryTake())
                {
                    connection.Enqueue(new ErrorMessage(SignalingErrorCodes.RateLimited));
                    continue;
                }

                if (message is null)
                {
                    connection.Enqueue(new ErrorMessage(SignalingErrorCodes.BadRequest));
                    continue;
                }

                Handle(connection, message);
            }

            closeStatus = WebSocketCloseStatus.PolicyViolation;
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            LogConnectionAborted(logger, ex.GetType().Name);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            foreach (var topic in hub.RemoveConnection(connection))
            {
                BroadcastPresence(topic);
            }

            connection.CompleteOutbound();
            try
            {
                await writer.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or TimeoutException)
            {
            }

            await session.CancelAsync().ConfigureAwait(false);
            await CloseAsync(socket, closeStatus).ConfigureAwait(false);
        }
    }

    private async Task<bool> HandshakeAsync(WebSocket socket, SignalingConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        using var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        helloTimeout.CancelAfter(options.HelloTimeout);
        SignalingMessage? first;
        try
        {
            (_, first) = await ReceiveAsync(socket, buffer, helloTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (first is not HelloMessage hello)
        {
            return false;
        }

        if (hello.Version < SignalingCodec.ProtocolVersion)
        {
            connection.Enqueue(new ErrorMessage(SignalingErrorCodes.UnsupportedVersion));
            return false;
        }

        // Newer clients are answered with our version; they decide whether they can speak it.
        connection.Enqueue(new WelcomeMessage(SignalingCodec.ProtocolVersion, SignalingCodec.ProtocolVersion));
        return true;
    }

    private void Handle(SignalingConnection connection, SignalingMessage message)
    {
        switch (message)
        {
            case SubscribeMessage subscribe:
                foreach (var topic in subscribe.Topics.Distinct(StringComparer.Ordinal))
                {
                    switch (hub.Subscribe(connection, topic))
                    {
                        case SubscribeResult.Subscribed:
                            BroadcastPresence(topic);
                            break;
                        case SubscribeResult.AlreadySubscribed:
                            connection.Enqueue(new PresenceMessage(topic, hub.Members(topic).Count - 1));
                            break;
                        case SubscribeResult.TopicFull:
                            connection.Enqueue(new ErrorMessage(SignalingErrorCodes.TopicFull));
                            break;
                        case SubscribeResult.TooManyTopics:
                            connection.Enqueue(new ErrorMessage(SignalingErrorCodes.TooManyTopics));
                            return;
                    }
                }

                break;
            case UnsubscribeMessage unsubscribe:
                foreach (var topic in unsubscribe.Topics)
                {
                    if (hub.Unsubscribe(connection, topic))
                    {
                        BroadcastPresence(topic);
                    }
                }

                break;
            case RelayMessage relay:
                var members = hub.Members(relay.Topic);
                if (!members.Contains(connection))
                {
                    connection.Enqueue(new ErrorMessage(SignalingErrorCodes.NotSubscribed));
                    break;
                }

                foreach (var member in members)
                {
                    if (!ReferenceEquals(member, connection))
                    {
                        member.Enqueue(relay);
                    }
                }

                break;
            case PingMessage:
                connection.Enqueue(new PongMessage());
                break;
            default:
                connection.Enqueue(new ErrorMessage(SignalingErrorCodes.BadRequest));
                break;
        }
    }

    private void BroadcastPresence(string topic)
    {
        var members = hub.Members(topic);
        foreach (var member in members)
        {
            member.Enqueue(new PresenceMessage(topic, members.Count - 1));
        }
    }

    /// <summary>Reads one complete text message. Returns a close status when the connection must end.</summary>
    private static async Task<(WebSocketCloseStatus? Close, SignalingMessage? Message)> ReceiveAsync(WebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        var length = 0;
        while (true)
        {
            if (length == SignalingCodec.MaxFrameBytes)
            {
                return (WebSocketCloseStatus.MessageTooBig, null);
            }

            var result = await socket.ReceiveAsync(buffer.AsMemory(length, SignalingCodec.MaxFrameBytes - length), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return (WebSocketCloseStatus.NormalClosure, null);
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                return (WebSocketCloseStatus.InvalidMessageType, null);
            }

            length += result.Count;
            if (result.EndOfMessage)
            {
                break;
            }
        }

        SignalingCodec.TryDeserialize(buffer.AsSpan(0, length), out var message);
        return (null, message);
    }

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await socket.CloseOutputAsync(status, null, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Signaling connection aborted ({Reason})")]
    private static partial void LogConnectionAborted(ILogger logger, string reason);
}
