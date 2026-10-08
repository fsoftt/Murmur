using System.Formats.Cbor;
using Directo.Protocol.Serialization;

namespace Directo.Protocol.Frames;

/// <summary>
/// CBOR codec for <see cref="PeerFrame"/>. Layout: map { 0: type, ...type specific fields }.
/// </summary>
public static class PeerFrameCodec
{
    private const uint KeyType = 0;
    private const uint KeyMessageId = 1;
    private const uint KeyLamport = 2;
    private const uint KeySentAt = 3;
    private const uint KeyBody = 4;

    private const uint TypeChatMessage = 1;
    private const uint TypeAck = 2;

    public static byte[] Encode(PeerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var writer = CborMap.CreateWriter();
        switch (frame)
        {
            case ChatMessageFrame message:
                if (System.Text.Encoding.UTF8.GetByteCount(message.Body) > ProtocolConstants.MaxMessageBodyBytes)
                {
                    throw new ProtocolException(ProtocolErrorCode.TooLarge, "Message body too large.");
                }

                if (message.Lamport < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(frame), "Lamport clock must be non-negative.");
                }

                writer.WriteStartMap(5);
                writer.WriteUInt32(KeyType);
                writer.WriteUInt32(TypeChatMessage);
                writer.WriteUInt32(KeyMessageId);
                CborMap.WriteGuid(writer, message.MessageId);
                writer.WriteUInt32(KeyLamport);
                writer.WriteInt64(message.Lamport);
                writer.WriteUInt32(KeySentAt);
                writer.WriteInt64(message.SentAtUnixMs);
                writer.WriteUInt32(KeyBody);
                writer.WriteTextString(message.Body);
                writer.WriteEndMap();
                break;
            case AckFrame ack:
                writer.WriteStartMap(2);
                writer.WriteUInt32(KeyType);
                writer.WriteUInt32(TypeAck);
                writer.WriteUInt32(KeyMessageId);
                CborMap.WriteGuid(writer, ack.MessageId);
                writer.WriteEndMap();
                break;
            default:
                throw new ArgumentException($"Cannot encode frame of type {frame.GetType().Name}.", nameof(frame));
        }

        return writer.Encode();
    }

    public static PeerFrame Decode(ReadOnlyMemory<byte> data)
    {
        var reader = CborMap.CreateReader(data, ProtocolConstants.MaxFrameBytes);
        uint? type = null;
        Guid? messageId = null;
        long? lamport = null;
        long? sentAt = null;
        string? body = null;

        CborMap.ReadMap(reader, (key, r) =>
        {
            switch (key)
            {
                case KeyType:
                    type = r.ReadUInt32();
                    return true;
                case KeyMessageId:
                    messageId = CborMap.ReadGuid(r, "messageId");
                    return true;
                case KeyLamport:
                    lamport = r.ReadInt64();
                    return true;
                case KeySentAt:
                    sentAt = r.ReadInt64();
                    return true;
                case KeyBody:
                    body = CborMap.ReadBoundedText(r, ProtocolConstants.MaxMessageBodyBytes, "body");
                    return true;
                default:
                    return false;
            }
        });
        CborMap.EnsureFullyConsumed(reader);

        switch (CborMap.Required(type, "type"))
        {
            case TypeChatMessage:
                var clock = CborMap.Required(lamport, "lamport");
                if (clock < 0)
                {
                    throw ProtocolException.Malformed("Lamport clock must be non-negative.");
                }

                return new ChatMessageFrame(
                    CborMap.Required(messageId, "messageId"),
                    clock,
                    CborMap.Required(sentAt, "sentAt"),
                    CborMap.Required(body, "body"));
            case TypeAck:
                return new AckFrame(CborMap.Required(messageId, "messageId"));
            case var unknown:
                return new UnknownFrame(unknown);
        }
    }
}
