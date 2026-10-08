using Murmur.Domain.Model;
using Murmur.IntegrationTests.Support;
using Murmur.Networking.Links;
using Xunit.Abstractions;

namespace Murmur.IntegrationTests;

/// <summary>The development-only transport used to try the app on devices before WebRTC exists.</summary>
public sealed class DevRelayTests(SignalingServerFixture server, ITestOutputHelper output) : IClassFixture<SignalingServerFixture>
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

    [Fact]
    public async Task A_backlog_larger_than_the_server_burst_is_paced_not_dropped()
    {
        var relay = new DevRelayPeerLinkFactory();
        await using var ana = await TestDevice.StartAsync("Ana", server, relay);
        await using var beto = await TestDevice.StartAsync("Beto", server, relay);
        var invite = await beto.Client.CreateInviteAsync();
        await ana.Client.AcceptInviteAsync(invite.Text);
        await Eventually.TrueAsync(async () => (await beto.Client.Contacts.ListAsync()).Count == 1, "Beto stored the contact");
        await beto.StopAsync();

        // The test server allows a burst of 50 relays per connection.
        var contact = await ana.SingleContactAsync();
        for (var i = 0; i < 80; i++)
        {
            await ana.Client.SendMessageAsync(contact.Id, $"m{i}");
        }

        await beto.StartAsync();

        try
        {
            await Eventually.TrueAsync(async () => (await beto.HistoryAsync()).Count == 80, "all 80 arrive", timeoutMs: 30_000);
        }
        catch
        {
            output.WriteLine($"Beto has {(await beto.HistoryAsync()).Count} messages");
            foreach (var line in ana.Logs.Lines.Concat(beto.Logs.Lines).Order(StringComparer.Ordinal))
            {
                output.WriteLine(line);
            }

            throw;
        }
        await Eventually.TrueAsync(async () => (await ana.HistoryAsync()).All(m => m.Status == MessageStatus.Delivered), "all 80 are confirmed", timeoutMs: 30_000);
    }
}
