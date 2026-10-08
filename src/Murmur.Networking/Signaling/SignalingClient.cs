using System.Net.WebSockets;
using Murmur.Domain.Common;
using Murmur.Protocol.Signaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Murmur.Networking.Signaling;

/// <summary>
/// WebSocket client for the signaling service. Reconnects forever with exponential backoff and
/// jitter, and re-establishes the declared subscriptions after every reconnection.
/// </summary>
public sealed partial class SignalingClient : ISignalingChannel, IAsyncDisposable
{
    private static readonly TimeSpan WelcomeTimeout = TimeSpan.FromSeconds(10);

    private readonly Uri _endpoint;
    private readonly Func<Uri, CancellationToken, Task<WebSocket>> _connect;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Backoff _backoff;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _presence = new(StringComparer.Ordinal);
    private readonly HashSet<string> _subscribed = new(StringComparer.Ordinal);
    private readonly AsyncSignal _syncNeeded = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private WebSocket? _socket;
    private Task? _loop;
    private SignalingState _state = SignalingState.Disconnected;

    public SignalingClient(
        Uri endpoint,
        Func<Uri, CancellationToken, Task<WebSocket>>? connect = null,
        TimeProvider? time = null,
        ILogger<SignalingClient>? logger = null,
        Backoff? backoff = null)
    {
        _endpoint = endpoint;
        _connect = connect ?? ConnectClientWebSocketAsync;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<SignalingClient>.Instance;
        _backoff = backoff ?? new Backoff(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
    }

    public SignalingState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public event EventHandler<SignalingState>? StateChanged;

    public event EventHandler<PresenceChange>? PresenceChanged;

    public event EventHandler<RelayReceived>? RelayReceived;

    public void Start()
    {
        lock (_gate)
        {
            _loop ??= Task.Run(() => RunAsync(_lifetime.Token));
        }
    }

    public void SetSubscriptions(string group, IReadOnlyCollection<string> topics)
    {
        foreach (var topic in topics)
        {
            if (!RendezvousTopicFormat.IsValid(topic))
            {
                throw new ArgumentException("Invalid rendezvous topic.", nameof(topics));
            }
        }

        lock (_gate)
        {
            if (topics.Count == 0)
            {
                _groups.Remove(group);
            }
            else
            {
                _groups[group] = new HashSet<string>(topics, StringComparer.Ordinal);
            }
        }

        _syncNeeded.Set();
    }

    public int GetPresence(string topic)
    {
        lock (_gate)
        {
            return _presence.GetValueOrDefault(topic);
        }
    }

    public async ValueTask<bool> TryRelayAsync(string topic, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        WebSocket? socket;
        lock (_gate)
        {
            socket = _socket;
        }

        if (socket is null)
        {
            return false;
        }

        try
        {
            await SendAsync(socket, new RelayMessage(topic, SignalingCodec.EncodeRelayData(data.Span)), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _lifetime.Dispose();
        _sendLock.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            SetState(SignalingState.Connecting);
            try
            {
                using var socket = await _connect(_endpoint, cancellationToken).ConfigureAwait(false);
                await SendAsync(socket, new HelloMessage(SignalingCodec.ProtocolVersion), cancellationToken).ConfigureAwait(false);
                using (var welcomeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    welcomeTimeout.CancelAfter(WelcomeTimeout);
                    if (await ReceiveAsync(socket, welcomeTimeout.Token).ConfigureAwait(false) is not WelcomeMessage)
                    {
                        throw new WebSocketException("Signaling handshake failed.");
                    }
                }

                lock (_gate)
                {
                    _socket = socket;
                    _subscribed.Clear();
                }

                SetState(SignalingState.Connected);
                attempt = 0;
                await RunConnectedAsync(socket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or HttpRequestException or InvalidOperationException)
            {
                LogConnectionFailed(_logger, ex.GetType().Name);
            }
            finally
            {
                OnDisconnected();
            }

            try
            {
                await Task.Delay(_backoff.Delay(attempt++, Random.Shared.NextDouble()), _time, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        SetState(SignalingState.Disconnected);
    }

    private async Task RunConnectedAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _syncNeeded.Set();
        var sync = SyncLoopAsync(socket, connection.Token);
        try
        {
            while (true)
            {
                var message = await ReceiveAsync(socket, connection.Token).ConfigureAwait(false);
                if (message is null)
                {
                    return;
                }

                Dispatch(message);
            }
        }
        finally
        {
            await connection.CancelAsync().ConfigureAwait(false);
            try
            {
                await sync.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
            }
        }
    }

    private async Task SyncLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            await _syncNeeded.WaitAsync(cancellationToken).ConfigureAwait(false);
            List<string> toAdd;
            List<string> toRemove;
            lock (_gate)
            {
                var desired = _groups.Values.SelectMany(t => t).ToHashSet(StringComparer.Ordinal);
                toAdd = desired.Where(t => !_subscribed.Contains(t)).ToList();
                toRemove = _subscribed.Where(t => !desired.Contains(t)).ToList();
                _subscribed.UnionWith(toAdd);
                _subscribed.ExceptWith(toRemove);
                foreach (var topic in toRemove)
                {
                    _presence.Remove(topic);
                }
            }

            foreach (var chunk in toRemove.Chunk(SignalingCodec.MaxTopicsPerMessage))
            {
                await SendAsync(socket, new UnsubscribeMessage(chunk), cancellationToken).ConfigureAwait(false);
            }

            foreach (var chunk in toAdd.Chunk(SignalingCodec.MaxTopicsPerMessage))
            {
                await SendAsync(socket, new SubscribeMessage(chunk), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Dispatch(SignalingMessage message)
    {
        switch (message)
        {
            case PresenceMessage presence:
                lock (_gate)
                {
                    if (!_subscribed.Contains(presence.Topic))
                    {
                        return;
                    }

                    _presence[presence.Topic] = presence.Peers;
                }

                PresenceChanged?.Invoke(this, new PresenceChange(presence.Topic, presence.Peers));
                break;
            case RelayMessage relay when SignalingCodec.TryDecodeRelayData(relay.Data, out var data):
                RelayReceived?.Invoke(this, new RelayReceived(relay.Topic, data));
                break;
            case ErrorMessage error:
                LogServerError(_logger, error.Code);
                break;
        }
    }

    private void OnDisconnected()
    {
        List<string> wasPresent;
        lock (_gate)
        {
            _socket = null;
            wasPresent = _presence.Where(p => p.Value > 0).Select(p => p.Key).ToList();
            _presence.Clear();
        }

        // Presence is unknown while offline. Existing peer-to-peer sessions are unaffected.
        foreach (var topic in wasPresent)
        {
            PresenceChanged?.Invoke(this, new PresenceChange(topic, 0));
        }

        SetState(SignalingState.Disconnected);
    }

    private void SetState(SignalingState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }

    private async Task SendAsync(WebSocket socket, SignalingMessage message, CancellationToken cancellationToken)
    {
        var payload = SignalingCodec.Serialize(message);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Returns the next valid message, or null when the server closed the connection.</summary>
    private static async Task<SignalingMessage?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[SignalingCodec.MaxFrameBytes];
        while (true)
        {
            var length = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                if (length == buffer.Length)
                {
                    throw new WebSocketException("Signaling message too large.");
                }

                result = await socket.ReceiveAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                length += result.Count;
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text && SignalingCodec.TryDeserialize(buffer.AsSpan(0, length), out var message))
            {
                return message;
            }
        }
    }

    private static async Task<WebSocket> ConnectClientWebSocketAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        socket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Signaling connection failed ({Reason})")]
    private static partial void LogConnectionFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Signaling server reported {Code}")]
    private static partial void LogServerError(ILogger logger, string code);
}
