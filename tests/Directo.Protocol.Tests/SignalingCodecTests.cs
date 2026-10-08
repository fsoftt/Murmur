using System.Text;
using Directo.Protocol.Signaling;

namespace Directo.Protocol.Tests;

public class SignalingCodecTests
{
    private static readonly string Topic = RendezvousTopicFormat.Encode(new byte[32]);

    [Fact]
    public void Messages_round_trip_with_discriminator()
    {
        SignalingMessage[] messages =
        [
            new HelloMessage(1),
            new WelcomeMessage(1, 1),
            new SubscribeMessage([Topic]),
            new UnsubscribeMessage([Topic]),
            new PresenceMessage(Topic, 1),
            new RelayMessage(Topic, SignalingCodec.EncodeRelayData([1, 2, 3])),
            new ErrorMessage(SignalingErrorCodes.RateLimited),
            new PingMessage(),
            new PongMessage(),
        ];

        foreach (var message in messages)
        {
            Assert.True(SignalingCodec.TryDeserialize(SignalingCodec.Serialize(message), out var decoded));
            Assert.Equal(message.GetType(), decoded!.GetType());
        }
    }

    [Fact]
    public void Wire_format_is_stable()
    {
        Assert.Equal("""{"t":"hello","v":1}""", Encoding.UTF8.GetString(SignalingCodec.Serialize(new HelloMessage(1))));
    }

    [Theory]
    [InlineData("""{"t":"sub","topics":["short"]}""")]
    [InlineData("""{"t":"sub","topics":[]}""")]
    [InlineData("""{"t":"relay","topic":"x","data":"AA"}""")]
    [InlineData("""{"t":"nope"}""")]
    [InlineData("""not json""")]
    [InlineData("""{"v":1}""")]
    public void Invalid_messages_are_rejected(string json)
    {
        Assert.False(SignalingCodec.TryDeserialize(Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void Oversized_relay_data_is_rejected()
    {
        var data = SignalingCodec.EncodeRelayData(new byte[SignalingCodec.MaxRelayDataBytes + 1]);
        var json = SignalingCodec.Serialize(new RelayMessage(Topic, data));

        Assert.False(SignalingCodec.TryDeserialize(json, out _));
    }

    [Fact]
    public void Discriminator_may_appear_after_other_properties()
    {
        Assert.True(SignalingCodec.TryDeserialize("""{"v":1,"t":"hello"}"""u8, out var message));
        Assert.Equal(new HelloMessage(1), message);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB", false)]
    public void Topic_format_is_validated(string? topic, bool valid)
    {
        Assert.Equal(valid, RendezvousTopicFormat.IsValid(topic));
    }
}
