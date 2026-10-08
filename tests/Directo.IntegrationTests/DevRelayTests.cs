using Directo.Domain.Model;
using Directo.IntegrationTests.Support;
using Directo.Networking.Links;

namespace Directo.IntegrationTests;

/// <summary>The development-only transport used to try the app on devices before WebRTC exists.</summary>
public sealed class DevRelayTests(SignalingServerFixture server) : IClassFixture<SignalingServerFixture>
{
    [Fact]
    public async Task Devices_pair_and_exchange_messages_through_the_dev_relay()
    {
        var relay = new DevRelayPeerLinkFactory();
        await using var ana = await TestDevice.StartAsync("Ana", server, relay);
        await using var beto = await TestDevice.StartAsync("Beto", server, relay);

        var invite = await beto.Client.CreateInviteAsync();
        await ana.Client.AcceptInviteAsync(invite.Text);
        await Eventually.TrueAsync(async () => (await beto.Client.Contacts.ListAsync()).Count == 1, "Beto stored the contact");

        // Larger than one relay chunk, so it is fragmented and reassembled.
        var big = new string('x', MessageRules.MaxBodyBytes);
        var sent = await ana.Client.SendMessageAsync((await ana.SingleContactAsync()).Id, big);

        await Eventually.TrueAsync(async () => (await ana.FindAsync(sent.Id))?.Status == MessageStatus.Delivered, "the big message is delivered");
        Assert.Equal(big, Assert.Single(await beto.HistoryAsync()).Body);
        Assert.DoesNotContain("xxxxxxxxxxxxxxxx", string.Join('\n', server.Sockets.Select(s => s.AllTraffic)), StringComparison.Ordinal);
    }
}
