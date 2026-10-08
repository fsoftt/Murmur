using System.Formats.Cbor;
using Murmur.Protocol.Frames;

namespace Murmur.Protocol.Tests;

public class PeerFrameCodecTests
{
    [Fact]
    public void Chat_message_round_trips()
    {
        var frame = new ChatMessageFrame(Guid.CreateVersion7(), 42, 1_700_000_000_123, "Hola 👋");

        var decoded = PeerFrameCodec.Decode(PeerFrameCodec.Encode(frame));

        Assert.Equal(frame, decoded);
    }

    [Fact]
    public void Ack_round_trips()
    {
        var frame = new AckFrame(Guid.NewGuid());

        Assert.Equal(frame, PeerFrameCodec.Decode(PeerFrameCodec.Encode(frame)));
    }

    [Fact]
    public void Unknown_fields_are_ignored_for_forward_compatibility()
    {
        var id = Guid.NewGuid();
        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(3);
        writer.WriteUInt32(0);
        writer.WriteUInt32(2);
        writer.WriteUInt32(1);
        writer.WriteByteString(id.ToByteArray(bigEndian: true));
        writer.WriteUInt32(99);
        writer.WriteTextString("future field");
        writer.WriteEndMap();

        Assert.Equal(new AckFrame(id), PeerFrameCodec.Decode(writer.Encode()));
    }

    [Fact]
    public void Unknown_frame_types_are_surfaced_not_rejected()
    {
        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(1);
        writer.WriteUInt32(0);
        writer.WriteUInt32(77);
        writer.WriteEndMap();

        Assert.Equal(new UnknownFrame(77), PeerFrameCodec.Decode(writer.Encode()));
    }

    [Fact]
    public void Oversized_body_is_rejected_on_encode()
    {
        var frame = new ChatMessageFrame(Guid.NewGuid(), 1, 0, new string('a', ProtocolConstants.MaxMessageBodyBytes + 1));

        var ex = Assert.Throws<ProtocolException>(() => PeerFrameCodec.Encode(frame));
        Assert.Equal(ProtocolErrorCode.TooLarge, ex.Code);
    }

    [Fact]
    public void Oversized_frame_is_rejected_before_parsing()
    {
        var ex = Assert.Throws<ProtocolException>(() => PeerFrameCodec.Decode(new byte[ProtocolConstants.MaxFrameBytes + 1]));
        Assert.Equal(ProtocolErrorCode.TooLarge, ex.Code);
    }

    [Fact]
    public void Missing_required_field_is_malformed()
    {
        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(1);
        writer.WriteUInt32(0);
        writer.WriteUInt32(2);
        writer.WriteEndMap();

        var ex = Assert.Throws<ProtocolException>(() => PeerFrameCodec.Decode(writer.Encode()));
        Assert.Equal(ProtocolErrorCode.Malformed, ex.Code);
    }

    [Fact]
    public void Trailing_bytes_are_rejected()
    {
        var bytes = PeerFrameCodec.Encode(new AckFrame(Guid.NewGuid()));

        Assert.Throws<ProtocolException>(() => PeerFrameCodec.Decode((byte[])[.. bytes, 0x00]));
    }

    [Fact]
    public void Duplicate_keys_are_rejected()
    {
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(3);
        writer.WriteUInt32(0);
        writer.WriteUInt32(2);
        writer.WriteUInt32(1);
        writer.WriteByteString(new byte[16]);
        writer.WriteUInt32(1);
        writer.WriteByteString(new byte[16]);
        writer.WriteEndMap();

        Assert.Throws<ProtocolException>(() => PeerFrameCodec.Decode(writer.Encode()));
    }

    [Fact]
    public void Random_input_only_ever_fails_with_protocol_exception()
    {
        var random = new Random(1234);
        var valid = PeerFrameCodec.Encode(new ChatMessageFrame(Guid.NewGuid(), 3, 5, "fuzz"));
        for (var i = 0; i < 20_000; i++)
        {
            byte[] input;
            if (i % 2 == 0)
            {
                input = new byte[random.Next(0, 64)];
                random.NextBytes(input);
            }
            else
            {
                input = (byte[])valid.Clone();
                input[random.Next(input.Length)] ^= (byte)(1 << random.Next(8));
            }

            try
            {
                PeerFrameCodec.Decode(input);
            }
            catch (ProtocolException)
            {
            }
        }
    }
}
