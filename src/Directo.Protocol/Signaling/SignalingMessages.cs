using System.Text.Json.Serialization;

namespace Directo.Protocol.Signaling;

/// <summary>
/// Messages exchanged with the signaling server over a WebSocket (JSON text frames).
/// The server only ever sees opaque rendezvous topics and opaque relay blobs.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "t")]
[JsonDerivedType(typeof(HelloMessage), "hello")]
[JsonDerivedType(typeof(WelcomeMessage), "welcome")]
[JsonDerivedType(typeof(SubscribeMessage), "sub")]
[JsonDerivedType(typeof(UnsubscribeMessage), "unsub")]
[JsonDerivedType(typeof(PresenceMessage), "presence")]
[JsonDerivedType(typeof(RelayMessage), "relay")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(PingMessage), "ping")]
[JsonDerivedType(typeof(PongMessage), "pong")]
public abstract record SignalingMessage;

/// <summary>Client → server. First message on every connection.</summary>
public sealed record HelloMessage([property: JsonPropertyName("v")] int Version) : SignalingMessage;

/// <summary>Server → client. Accepts the session and announces the supported range.</summary>
public sealed record WelcomeMessage(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("minV")] int MinVersion) : SignalingMessage;

/// <summary>Client → server. Start receiving presence and relays for the topics.</summary>
public sealed record SubscribeMessage([property: JsonPropertyName("topics")] IReadOnlyList<string> Topics) : SignalingMessage;

/// <summary>Client → server.</summary>
public sealed record UnsubscribeMessage([property: JsonPropertyName("topics")] IReadOnlyList<string> Topics) : SignalingMessage;

/// <summary>Server → client. Number of other connections subscribed to <paramref name="Topic"/>.</summary>
public sealed record PresenceMessage(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("peers")] int Peers) : SignalingMessage;

/// <summary>Both directions. Opaque base64url blob forwarded to the other subscribers of the topic.</summary>
public sealed record RelayMessage(
    [property: JsonPropertyName("topic")] string Topic,
    [property: JsonPropertyName("data")] string Data) : SignalingMessage;

/// <summary>Server → client.</summary>
public sealed record ErrorMessage(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("detail")] string? Detail = null) : SignalingMessage;

public sealed record PingMessage : SignalingMessage;

public sealed record PongMessage : SignalingMessage;

public static class SignalingErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string UnsupportedVersion = "unsupported_version";
    public const string RateLimited = "rate_limited";
    public const string TooManyTopics = "too_many_topics";
    public const string TopicFull = "topic_full";
    public const string NotSubscribed = "not_subscribed";
    public const string PayloadTooLarge = "payload_too_large";
}
