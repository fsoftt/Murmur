using Directo.Protocol.Frames;
using Directo.Protocol.Identity;

namespace Directo.Protocol.Tests;

public class HandshakePayloadTests
{
    [Fact]
    public void Payload_with_pairing_fields_round_trips()
    {
        var card = new IdentityCard(Enumerable.Repeat((byte)1, 32).ToArray(), Enumerable.Repeat((byte)2, 32).ToArray(), new byte[64]);
        var payload = new HandshakePayload(1, 3, ["text"], card, new byte[16], "Ana");

        var decoded = HandshakePayloadCodec.Decode(HandshakePayloadCodec.Encode(payload));

        Assert.Equal(1, decoded.MinVersion);
        Assert.Equal(3, decoded.MaxVersion);
        Assert.Equal(["text"], decoded.Capabilities);
        Assert.Equal(card.IdentityKey, decoded.Card!.IdentityKey);
        Assert.Equal(card.StaticKey, decoded.Card.StaticKey);
        Assert.Equal(new byte[16], decoded.PairingToken);
        Assert.Equal("Ana", decoded.ProfileName);
    }

    [Theory]
    [InlineData(1, 1, 1, 1, 1)]
    [InlineData(1, 3, 2, 5, 3)]
    [InlineData(2, 4, 1, 2, 2)]
    public void Negotiation_picks_highest_common_version(int localMin, int localMax, int remoteMin, int remoteMax, int expected)
    {
        var local = new HandshakePayload(localMin, localMax, []);
        var remote = new HandshakePayload(remoteMin, remoteMax, []);

        Assert.Equal(expected, HandshakePayload.Negotiate(local, remote));
    }

    [Fact]
    public void Negotiation_fails_without_overlap()
    {
        var ex = Assert.Throws<ProtocolException>(() =>
            HandshakePayload.Negotiate(new HandshakePayload(1, 1, []), new HandshakePayload(2, 3, [])));

        Assert.Equal(ProtocolErrorCode.UnsupportedVersion, ex.Code);
    }
}
