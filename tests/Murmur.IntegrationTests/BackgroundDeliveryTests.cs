using Murmur.Domain.Model;
using Murmur.IntegrationTests.Support;
using Murmur.Networking.Links;
using Murmur.Networking.Signaling;

namespace Murmur.IntegrationTests;

/// <summary>Option A: a background job delivers pending messages without the app being opened.</summary>
public sealed class BackgroundDeliveryTests(SignalingServerFixture server) : IClassFixture<SignalingServerFixture>, IAsyncLifetime
{
    private readonly InMemoryPeerLinkNetwork _network = new();
    private TestDevice _ana = null!;
    private TestDevice _beto = null!;

    public async Task InitializeAsync()
    {
        _ana = await TestDevice.StartAsync("Ana", server, _network);
        _beto = await TestDevice.StartAsync("Beto", server, _network);
        var invite = await _beto.Client.CreateInviteAsync();
        await _ana.Client.AcceptInviteAsync(invite.Text);
        await Eventually.TrueAsync(async () => (await _beto.Client.Contacts.ListAsync()).Count == 1, "Beto stored the contact");
    }

    public async Task DisposeAsync()
    {
        await _ana.DisposeAsync();
        await _beto.DisposeAsync();
    }

    [Fact]
    public async Task Background_pass_delivers_while_the_app_stays_closed()
    {
        await _beto.StopAsync();
        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "enviado con la app cerrada");
        await _ana.StopAsync();
        await _beto.StartAsync();

        // Android starts the process only for the job: no UI, networking not started.
        await _ana.OpenAsync(start: false);
        var remaining = await _ana.Client.DeliverPendingAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(0, remaining);
        Assert.Equal(MessageStatus.Delivered, (await _ana.FindAsync(message.Id))!.Status);
        Assert.Equal("enviado con la app cerrada", Assert.Single(await _beto.HistoryAsync()).Body);
    }

    [Fact]
    public async Task Background_pass_gives_up_within_its_budget_when_the_recipient_is_unreachable()
    {
        await _beto.StopAsync();
        await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "nadie escucha");

        var started = System.Diagnostics.Stopwatch.StartNew();
        var remaining = await _ana.Client.DeliverPendingAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, remaining);
        Assert.InRange(started.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Background_pass_does_nothing_when_nothing_is_pending()
    {
        await _ana.StopAsync();
        await _ana.OpenAsync(start: false);

        Assert.Equal(0, await _ana.Client.DeliverPendingAsync(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(SignalingState.Connected, _ana.Client.SignalingState);
    }
}
