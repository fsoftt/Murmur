using Murmur.Domain.Connections;
using Murmur.Domain.Model;
using Murmur.IntegrationTests.Support;
using Murmur.Networking.Links;
using Murmur.Protocol;

namespace Murmur.IntegrationTests;

/// <summary>
/// Full devices (identity, encrypted database, real signaling server, Noise sessions) over a
/// simulated peer-to-peer network. These are the scenarios from architecture §39.
/// </summary>
public sealed class EndToEndTests(SignalingServerFixture server) : IClassFixture<SignalingServerFixture>, IAsyncLifetime
{
    private readonly InMemoryPeerLinkNetwork _network = new();
    private TestDevice _ana = null!;
    private TestDevice _beto = null!;

    public async Task InitializeAsync()
    {
        _ana = await TestDevice.StartAsync("Ana", server, _network);
        _beto = await TestDevice.StartAsync("Beto", server, _network);
    }

    public async Task DisposeAsync()
    {
        await _ana.DisposeAsync();
        await _beto.DisposeAsync();
    }

    [Fact]
    public async Task Qr_pairing_creates_mutual_contacts_with_the_same_safety_number()
    {
        await PairAsync();

        var anasView = await _ana.SingleContactAsync();
        var betosView = await _beto.SingleContactAsync();
        Assert.Equal("Beto", anasView.DisplayName);
        Assert.Equal("Ana", betosView.DisplayName);
        Assert.Equal(_beto.Client.IdentityKey, anasView.Identity.IdentityKey);
        Assert.Equal(_ana.Client.IdentityKey, betosView.Identity.IdentityKey);
        Assert.Equal(VerificationState.Unverified, anasView.Verification);
        Assert.Equal(
            await _ana.Client.GetSafetyNumberAsync(anasView.Id),
            await _beto.Client.GetSafetyNumberAsync(betosView.Id));
    }

    [Fact]
    public async Task Messages_flow_both_ways_and_are_confirmed()
    {
        await PairAsync();

        var hola = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "Hola Beto");
        var reply = await _beto.Client.SendMessageAsync((await _beto.SingleContactAsync()).Id, "Hola Ana");

        await WaitDeliveredAsync(_ana, hola.Id);
        await WaitDeliveredAsync(_beto, reply.Id);
        Assert.Equal(["Hola Ana", "Hola Beto"], (await _ana.HistoryAsync()).Select(m => m.Body).Order(StringComparer.Ordinal));
        Assert.Equal(PeerConnectionState.Connected, _ana.Client.GetConnectionState((await _ana.SingleContactAsync()).Id));
    }

    [Fact]
    public async Task Message_waits_on_the_sender_until_the_recipient_comes_online()
    {
        await PairAsync();
        await _beto.StopAsync();

        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "¿Estás?");
        await Task.Delay(500);
        Assert.Equal(MessageStatus.Pending, (await _ana.FindAsync(message.Id))!.Status);

        await _beto.StartAsync();

        await WaitDeliveredAsync(_ana, message.Id);
        Assert.Equal("¿Estás?", Assert.Single(await _beto.HistoryAsync()).Body);
    }

    [Fact]
    public async Task Nothing_is_delivered_while_the_sender_is_offline()
    {
        await PairAsync();
        await _beto.StopAsync();
        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "escrito sin conexión");
        await _ana.StopAsync();

        await _beto.StartAsync();
        await Task.Delay(1000);
        Assert.Empty(await _beto.HistoryAsync());

        await _ana.StartAsync();
        await WaitDeliveredAsync(_ana, message.Id);
        Assert.Single(await _beto.HistoryAsync());
    }

    [Fact]
    public async Task Network_drop_recovers_without_duplicates()
    {
        await PairAsync();
        var contact = await _ana.SingleContactAsync();
        var first = await _ana.Client.SendMessageAsync(contact.Id, "uno");
        await WaitDeliveredAsync(_ana, first.Id);

        _network.DropAllLinks();
        var second = await _ana.Client.SendMessageAsync(contact.Id, "dos");

        await WaitDeliveredAsync(_ana, second.Id);
        Assert.Equal(["uno", "dos"], (await _beto.HistoryAsync()).Select(m => m.Body));
        Assert.True(_network.ConnectionsEstablished >= 2);
    }

    [Fact]
    public async Task Unreachable_peer_keeps_messages_pending_until_a_direct_path_exists()
    {
        await PairAsync();
        _network.Reachable = false;
        _network.DropAllLinks();
        var contact = await _ana.SingleContactAsync();
        await Eventually.TrueAsync(() => _ana.Client.GetConnectionState(contact.Id) != PeerConnectionState.Connected, "the session is gone");

        var message = await _ana.Client.SendMessageAsync(contact.Id, "sin TURN");
        await Task.Delay(800);
        Assert.Equal(MessageStatus.Pending, (await _ana.FindAsync(message.Id))!.Status);
        Assert.NotEqual(PeerConnectionState.Connected, _ana.Client.GetConnectionState(contact.Id));

        _network.Reachable = true;
        await WaitDeliveredAsync(_ana, message.Id);
    }

    [Fact]
    public async Task Blocked_contact_gets_nothing()
    {
        await PairAsync();
        await _beto.Client.Contacts.SetBlockedAsync((await _beto.SingleContactAsync()).Id, true);
        await Eventually.TrueAsync(
            async () => _ana.Client.GetConnectionState((await _ana.SingleContactAsync()).Id) != PeerConnectionState.Connected,
            "the session is torn down");

        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "¿me bloqueaste?");
        await Task.Delay(800);

        // Ana may have transmitted it into a session Beto refused at once ("Sent"), but it is
        // never confirmed, and Beto's phone stores nothing.
        Assert.Contains((await _ana.FindAsync(message.Id))!.Status, new[] { MessageStatus.Pending, MessageStatus.Sent });
        Assert.Empty(await _beto.HistoryAsync());
    }

    [Fact]
    public async Task Signaling_server_never_sees_message_content_or_identity_keys()
    {
        await PairAsync();
        const string secret = "contenido-que-el-servidor-no-debe-ver";
        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, secret);
        await WaitDeliveredAsync(_ana, message.Id);

        var traffic = string.Join('\n', server.Sockets.Select(s => s.AllTraffic));
        Assert.DoesNotContain(secret, traffic, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", traffic, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(_ana.Client.IdentityKey.ToArray()), traffic, StringComparison.Ordinal);
        Assert.DoesNotContain(System.Buffers.Text.Base64Url.EncodeToString(_ana.Client.IdentityKey.Span), traffic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invites_cannot_be_reused_by_a_third_device()
    {
        var invite = await _beto.Client.CreateInviteAsync();
        await _ana.Client.AcceptInviteAsync(invite.Text);
        await using var carla = await TestDevice.StartAsync("Carla", server, _network);

        // The inviter stopped listening once the invite was used, so nobody answers Carla.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => carla.Client.Pairing.AcceptInviteAsync(invite.Text, "Carla", timeout.Token));

        Assert.Empty(await carla.Client.Contacts.ListAsync());
        Assert.Single(await _beto.Client.Contacts.ListAsync());
    }

    [Fact]
    public async Task Own_and_tampered_invites_are_rejected()
    {
        var invite = await _ana.Client.CreateInviteAsync();

        var own = await Assert.ThrowsAsync<MurmurException>(() => _ana.Client.AcceptInviteAsync(invite.Text));
        Assert.Equal(MurmurErrorCode.InviteInvalid, own.Code);

        var tampered = invite.Text[..^4] + (invite.Text[^4] == 'A' ? "B" : "A") + invite.Text[^3..];
        var bad = await Assert.ThrowsAsync<MurmurException>(() => _beto.Client.AcceptInviteAsync(tampered));
        Assert.Equal(MurmurErrorCode.InviteInvalid, bad.Code);
    }

    [Fact]
    public async Task Pairing_fails_cleanly_when_the_inviter_is_offline()
    {
        var invite = await _beto.Client.CreateInviteAsync();
        await _beto.StopAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<Exception>(() => _ana.Client.AcceptInviteAsync(invite.Text, timeout.Token));

        Assert.Empty(await _ana.Client.Contacts.ListAsync());
    }

    [Fact]
    public async Task Pairing_survives_restarts_and_identity_is_stable()
    {
        await PairAsync();
        var identityBefore = _ana.Client.IdentityKey;

        await _ana.StopAsync();
        await _ana.StartAsync();

        Assert.Equal(identityBefore, _ana.Client.IdentityKey);
        var message = await _ana.Client.SendMessageAsync((await _ana.SingleContactAsync()).Id, "después de reiniciar");
        await WaitDeliveredAsync(_ana, message.Id);
    }

    [Fact]
    public void Domain_and_protocol_size_limits_agree() =>
        Assert.Equal(ProtocolConstants.MaxMessageBodyBytes, MessageRules.MaxBodyBytes);

    private async Task PairAsync()
    {
        var invite = await _beto.Client.CreateInviteAsync();
        var paired = new TaskCompletionSource<Contact>();
        _beto.Client.Pairing.ContactPaired += (_, c) => paired.TrySetResult(c);

        var contact = await _ana.Client.AcceptInviteAsync(invite.Text);

        Assert.Equal(_beto.Client.IdentityKey, contact.Identity.IdentityKey);
        await paired.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static Task WaitDeliveredAsync(TestDevice device, MessageId id) =>
        Eventually.TrueAsync(async () => (await device.FindAsync(id))?.Status == MessageStatus.Delivered, $"message {id} is delivered");
}
