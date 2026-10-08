using Murmur.Security.Noise;
using Murmur.Security.Primitives;

namespace Murmur.Security.Tests;

public class NoiseHandshakeTests
{
    private static readonly byte[] Prologue = "test"u8.ToArray();

    [Fact]
    public void KK_with_fresh_ephemerals_establishes_a_working_channel()
    {
        var (a, aPub) = NewKey();
        var (b, bPub) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.KK, true, Prologue, a, bPub);
        using var responder = new HandshakeState(HandshakePattern.KK, false, Prologue, b, aPub);

        responder.ReadMessage(initiator.WriteMessage([]));
        initiator.ReadMessage(responder.WriteMessage([]));

        using var t1 = initiator.Split();
        using var t2 = responder.Split();
        Assert.Equal("hi"u8.ToArray(), t2.Decrypt(t1.Encrypt("hi"u8)));
        Assert.Equal("yo"u8.ToArray(), t1.Decrypt(t2.Encrypt("yo"u8)));
    }

    [Fact]
    public void KK_fails_when_responder_expects_a_different_initiator()
    {
        var (a, _) = NewKey();
        var (b, bPub) = NewKey();
        var (_, mallory) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.KK, true, Prologue, a, bPub);
        using var responder = new HandshakeState(HandshakePattern.KK, false, Prologue, b, mallory);

        Assert.Throws<CryptoException>(() => responder.ReadMessage(initiator.WriteMessage([])));
    }

    [Fact]
    public void Prologue_mismatch_fails_the_handshake()
    {
        var (a, aPub) = NewKey();
        var (b, bPub) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.KK, true, "v1"u8, a, bPub);
        using var responder = new HandshakeState(HandshakePattern.KK, false, "v2"u8, b, aPub);

        Assert.Throws<CryptoException>(() => responder.ReadMessage(initiator.WriteMessage([])));
    }

    [Fact]
    public void Tampered_handshake_message_is_rejected()
    {
        var (a, aPub) = NewKey();
        var (b, bPub) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.IK, true, Prologue, a, bPub);
        using var responder = new HandshakeState(HandshakePattern.IK, false, Prologue, b);

        var message = initiator.WriteMessage("payload"u8);
        message[^1] ^= 1;

        Assert.Throws<CryptoException>(() => responder.ReadMessage(message));
        Assert.Throws<InvalidOperationException>(() => responder.WriteMessage([]));
    }

    [Fact]
    public void Transport_rejects_replayed_and_reordered_messages()
    {
        var (t1, t2) = Establish();
        var first = t1.Encrypt("1"u8);
        var second = t1.Encrypt("2"u8);

        Assert.Throws<CryptoException>(() => t2.Decrypt(second));
        Assert.Equal("1"u8.ToArray(), t2.Decrypt(first));
        Assert.Throws<CryptoException>(() => t2.Decrypt(first));
    }

    [Fact]
    public void Out_of_turn_operations_are_refused()
    {
        var (a, _) = NewKey();
        var (_, bPub) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.KK, true, Prologue, a, bPub);

        Assert.Throws<InvalidOperationException>(() => initiator.ReadMessage(new byte[64]));
        Assert.Throws<InvalidOperationException>(() => initiator.Split());
    }

    [Fact]
    public void Low_order_public_keys_are_rejected()
    {
        Assert.Throws<CryptoException>(() => Curve25519.X25519Agreement(Curve25519.NewPrivateKey(), new byte[32]));
    }

    private static (NoiseTransport, NoiseTransport) Establish()
    {
        var (a, aPub) = NewKey();
        var (b, bPub) = NewKey();
        using var initiator = new HandshakeState(HandshakePattern.KK, true, Prologue, a, bPub);
        using var responder = new HandshakeState(HandshakePattern.KK, false, Prologue, b, aPub);
        responder.ReadMessage(initiator.WriteMessage([]));
        initiator.ReadMessage(responder.WriteMessage([]));
        return (initiator.Split(), responder.Split());
    }

    private static (byte[] Private, byte[] Public) NewKey()
    {
        var key = Curve25519.NewPrivateKey();
        return (key, Curve25519.X25519PublicKey(key));
    }
}
