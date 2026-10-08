using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Directo.Protocol.Signaling;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    MaxDepth = 8)]
[JsonSerializable(typeof(SignalingMessage))]
internal sealed partial class SignalingJsonContext : JsonSerializerContext;

public static class SignalingCodec
{
    public const int ProtocolVersion = 1;

    /// <summary>Maximum decoded size of a relay blob.</summary>
    public const int MaxRelayDataBytes = 16 * 1024;

    /// <summary>Maximum size of a single JSON text frame.</summary>
    public const int MaxFrameBytes = 32 * 1024;

    /// <summary>Maximum topics in a single subscribe/unsubscribe message.</summary>
    public const int MaxTopicsPerMessage = 256;

    public static byte[] Serialize(SignalingMessage message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, SignalingJsonContext.Default.SignalingMessage);

    /// <summary>Parses and validates a frame. Returns false for anything malformed or out of bounds.</summary>
    public static bool TryDeserialize(ReadOnlySpan<byte> utf8Json, out SignalingMessage? message)
    {
        message = null;
        if (utf8Json.Length is 0 or > MaxFrameBytes)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize(utf8Json, SignalingJsonContext.Default.SignalingMessage);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return false;
        }

        if (message is null || !IsValid(message))
        {
            message = null;
            return false;
        }

        return true;
    }

    public static string EncodeRelayData(ReadOnlySpan<byte> data) => Base64Url.EncodeToString(data);

    public static bool TryDecodeRelayData(string data, out byte[] bytes)
    {
        bytes = [];
        if (Base64Url.GetMaxDecodedLength(data.Length) > MaxRelayDataBytes + 2)
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(data);
            return bytes.Length <= MaxRelayDataBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValid(SignalingMessage message) => message switch
    {
        SubscribeMessage m => m.Topics is { Count: > 0 and <= MaxTopicsPerMessage } && m.Topics.All(RendezvousTopicFormat.IsValid),
        UnsubscribeMessage m => m.Topics is { Count: > 0 and <= MaxTopicsPerMessage } && m.Topics.All(RendezvousTopicFormat.IsValid),
        RelayMessage m => RendezvousTopicFormat.IsValid(m.Topic) && m.Data is not null && TryDecodeRelayData(m.Data, out _),
        PresenceMessage m => RendezvousTopicFormat.IsValid(m.Topic) && m.Peers >= 0,
        ErrorMessage m => m.Code is { Length: > 0 and <= 64 } && (m.Detail is null || m.Detail.Length <= 256),
        _ => true,
    };
}

/// <summary>A rendezvous topic is 32 random-looking bytes encoded as unpadded base64url (43 chars).</summary>
public static class RendezvousTopicFormat
{
    public const int TopicBytes = 32;
    public const int EncodedLength = 43;

    public static string Encode(ReadOnlySpan<byte> topic)
    {
        if (topic.Length != TopicBytes)
        {
            throw new ArgumentException($"Topic must be {TopicBytes} bytes.", nameof(topic));
        }

        return Base64Url.EncodeToString(topic);
    }

    public static bool IsValid(string? topic)
    {
        if (topic is null || topic.Length != EncodedLength)
        {
            return false;
        }

        foreach (var c in topic)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')
            {
                return false;
            }
        }

        // Reject non-canonical encodings (unused trailing bits set) so one topic has one spelling.
        // Base64Url.TryDecodeFromChars throws instead of returning false for non-canonical input.
        Span<byte> buffer = stackalloc byte[TopicBytes + 1];
        try
        {
            return Base64Url.TryDecodeFromChars(topic, buffer, out var written)
                && written == TopicBytes
                && Base64Url.EncodeToString(buffer[..TopicBytes]) == topic;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
