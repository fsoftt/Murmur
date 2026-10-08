using Murmur.Protocol;
using Murmur.Protocol.Identity;
using Murmur.Security.Identity;

namespace Murmur.Security.Tests;

public class IdentityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Keys_survive_serialization()
    {
        using var keys = LocalIdentityKeys.Generate();
        using var restored = LocalIdentityKeys.Deserialize(keys.Serialize());

        Assert.Equal(keys.IdentityPublicKey, restored.IdentityPublicKey);
        Assert.Equal(keys.StaticPublicKey, restored.StaticPublicKey);
        Assert.True(IdentityCardVerifier.IsValid(restored.Card));
    }

    [Fact]
    public void Card_with_swapped_static_key_is_invalid()
    {
        using var alice = LocalIdentityKeys.Generate();
        using var mallory = LocalIdentityKeys.Generate();

        var forged = alice.Card with { StaticKey = mallory.StaticPublicKey };

        Assert.False(IdentityCardVerifier.IsValid(forged));
    }

    [Fact]
    public void Invite_round_trips_and_verifies()
    {
        using var keys = LocalIdentityKeys.Generate();
        var created = InviteService.Create(keys, Now, "Bea");

        var verified = InviteService.Verify(created.Text, Now.AddMinutes(1));

        Assert.StartsWith(InviteCodec.Prefix, created.Text, StringComparison.Ordinal);
        Assert.Equal(keys.IdentityPublicKey, verified.Card.IdentityKey);
        Assert.Equal(created.Token, verified.Token);
        Assert.Equal("Bea", verified.ProfileName);
        Assert.True(created.Text.Length < 400, $"Invite should fit comfortably in a QR code ({created.Text.Length} chars).");
    }

    [Fact]
    public void Expired_invite_is_rejected()
    {
        using var keys = LocalIdentityKeys.Generate();
        var created = InviteService.Create(keys, Now);

        var ex = Assert.Throws<ProtocolException>(() => InviteService.Verify(created.Text, Now + InviteService.DefaultLifetime + InviteService.ClockSkew + TimeSpan.FromSeconds(1)));
        Assert.Equal(ProtocolErrorCode.Expired, ex.Code);
    }

    [Fact]
    public void Invite_signed_by_another_identity_is_rejected()
    {
        using var alice = LocalIdentityKeys.Generate();
        using var mallory = LocalIdentityKeys.Generate();
        var body = InviteCodec.EncodeBody(new InviteBody(alice.Card, new byte[16], Now.AddMinutes(5), null));
        var text = InviteCodec.Encode(body, mallory.Sign(SignedInvite.ToBeSigned(body)));

        var ex = Assert.Throws<ProtocolException>(() => InviteService.Verify(text, Now));
        Assert.Equal(ProtocolErrorCode.InvalidSignature, ex.Code);
    }

    [Fact]
    public void Any_bit_flip_in_an_invite_is_detected()
    {
        using var keys = LocalIdentityKeys.Generate();
        var text = InviteService.Create(keys, Now).Text;
        var raw = System.Buffers.Text.Base64Url.DecodeFromChars(text.AsSpan(InviteCodec.Prefix.Length));

        for (var i = 0; i < raw.Length * 8; i += 7)
        {
            var copy = (byte[])raw.Clone();
            copy[i / 8] ^= (byte)(1 << (i % 8));
            var tampered = InviteCodec.Prefix + System.Buffers.Text.Base64Url.EncodeToString(copy);
            Assert.Throws<ProtocolException>(() => InviteService.Verify(tampered, Now));
        }
    }

    [Fact]
    public void Long_profile_names_are_truncated_to_the_protocol_limit()
    {
        var name = InviteService.NormalizeProfileName(new string('ñ', 100));

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(name!) <= ProtocolConstants.MaxProfileNameBytes);
    }

    [Fact]
    public void Safety_number_is_symmetric_and_key_dependent()
    {
        using var a = LocalIdentityKeys.Generate();
        using var b = LocalIdentityKeys.Generate();
        using var c = LocalIdentityKeys.Generate();

        var ab = SafetyNumber.Compute(a.IdentityPublicKey, b.IdentityPublicKey);

        Assert.Equal(ab, SafetyNumber.Compute(b.IdentityPublicKey, a.IdentityPublicKey));
        Assert.NotEqual(ab, SafetyNumber.Compute(a.IdentityPublicKey, c.IdentityPublicKey));
        Assert.Matches(@"^(\d{5} ){11}\d{5}$", ab);
    }

    [Fact]
    public void Both_contacts_derive_the_same_rotating_topic()
    {
        using var a = LocalIdentityKeys.Generate();
        using var b = LocalIdentityKeys.Generate();

        var fromA = Rendezvous.ContactTopic(a, b.StaticPublicKey, 100);
        var fromB = Rendezvous.ContactTopic(b, a.StaticPublicKey, 100);

        Assert.Equal(fromA, fromB);
        Assert.NotEqual(fromA, Rendezvous.ContactTopic(a, b.StaticPublicKey, 101));
        Assert.True(Protocol.Signaling.RendezvousTopicFormat.IsValid(fromA));
    }

    [Fact]
    public void Active_epochs_overlap_near_day_boundaries()
    {
        var midday = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var lateNight = new DateTimeOffset(2026, 10, 8, 23, 30, 0, TimeSpan.Zero);
        var earlyMorning = new DateTimeOffset(2026, 10, 9, 0, 30, 0, TimeSpan.Zero);
        var today = Rendezvous.EpochOf(midday);

        Assert.Equal([today], Rendezvous.ActiveEpochs(midday));
        Assert.Equal([today, today + 1], Rendezvous.ActiveEpochs(lateNight));
        Assert.Equal([today, today + 1], Rendezvous.ActiveEpochs(earlyMorning));
    }

    [Fact]
    public void Exactly_one_side_initiates()
    {
        using var a = LocalIdentityKeys.Generate();
        using var b = LocalIdentityKeys.Generate();

        Assert.NotEqual(
            KeyOrdering.IsInitiator(a.StaticPublicKey, b.StaticPublicKey),
            KeyOrdering.IsInitiator(b.StaticPublicKey, a.StaticPublicKey));
    }
}
