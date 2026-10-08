using Directo.Core.Tests.Support;
using Directo.Domain.Common;
using Directo.Domain.Delivery;
using Directo.Domain.Model;
using Directo.Domain.Ports;
using Directo.Domain.UseCases;

namespace Directo.Core.Tests;

/// <summary>Two devices, each with its own encrypted database, talking over an in-memory channel.</summary>
public sealed class DeliveryTests : IAsyncLifetime
{
    private static readonly DeliveryOptions FastRetransmission = new() { Retransmission = new Backoff(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(400)) };

    private Device _alice = null!;
    private Device _bob = null!;

    public async Task InitializeAsync()
    {
        _alice = await Device.CreateAsync("Bob");
        _bob = await Device.CreateAsync("Alice");
    }

    public async Task DisposeAsync()
    {
        await _alice.DisposeAsync();
        await _bob.DisposeAsync();
    }

    [Fact]
    public async Task Messages_written_offline_are_delivered_when_both_are_online()
    {
        var offline = await _alice.SendAsync("written while Bob was offline");
        Assert.Equal(MessageStatus.Pending, offline.Status);

        await using var link = Connect();

        await _alice.WaitDeliveredAsync(offline.Id);
        var received = Assert.Single(await _bob.HistoryAsync());
        Assert.Equal("written while Bob was offline", received.Body);
        Assert.Equal(MessageStatus.Received, received.Status);
    }

    [Fact]
    public async Task Messages_sent_during_a_session_are_delivered_immediately()
    {
        await using var link = Connect();

        var message = await _alice.SendAsync("live");

        await _alice.WaitDeliveredAsync(message.Id);
    }

    [Fact]
    public async Task Lost_ack_causes_retransmission_but_no_duplicate()
    {
        var message = await _alice.SendAsync("ack will be lost once");
        var dropped = 0;
        await using var link = Connect(bobFilter: e => !(e is PeerAckReceived && Interlocked.Increment(ref dropped) == 1));

        await _alice.WaitDeliveredAsync(message.Id);

        Assert.True(link.AliceChannel.MessagesSent >= 2, "Alice should have retransmitted.");
        Assert.Single(await _bob.HistoryAsync());
    }

    [Fact]
    public async Task Hundred_messages_arrive_once_and_in_order()
    {
        var sent = new List<Message>();
        for (var i = 0; i < 100; i++)
        {
            sent.Add(await _alice.SendAsync($"message {i:D3}"));
        }

        await using var link = Connect();
        await _alice.WaitDeliveredAsync(sent[^1].Id);
        await Eventually.TrueAsync(async () => (await _alice.Store.Messages.ListOutboxAsync(_alice.Conversation.Id)).Count == 0, "all delivered");

        var history = await _bob.HistoryAsync(200);
        Assert.Equal(sent.Select(m => m.Body), history.Select(m => m.Body));
    }

    [Fact]
    public async Task Connection_drop_and_reconnect_resumes_without_duplicates()
    {
        var first = await _alice.SendAsync("before drop");
        var link = Connect(bobFilter: e => e is not PeerAckReceived);
        await Eventually.TrueAsync(async () => (await _bob.HistoryAsync()).Count == 1, "Bob stored the message");
        await link.DisposeAsync();

        Assert.Equal(MessageStatus.Sent, (await _alice.Store.Messages.GetAsync(_alice.Conversation.Id, first.Id))!.Status);

        await using var second = Connect();
        await _alice.WaitDeliveredAsync(first.Id);
        Assert.Single(await _bob.HistoryAsync());
    }

    [Fact]
    public async Task Outbox_survives_an_app_restart()
    {
        var message = await _alice.SendAsync("survives restart");
        await _alice.RestartAsync();

        await using var link = Connect();

        await _alice.WaitDeliveredAsync(message.Id);
    }

    [Fact]
    public async Task Both_sides_agree_on_message_order_with_concurrent_writes()
    {
        for (var i = 0; i < 10; i++)
        {
            await _alice.SendAsync($"a{i}");
            await _bob.SendAsync($"b{i}");
        }

        await using var link = Connect();
        await Eventually.TrueAsync(async () => (await _alice.HistoryAsync()).Count == 20 && (await _bob.HistoryAsync()).Count == 20, "both have everything");

        Assert.Equal((await _alice.HistoryAsync()).Select(m => m.Id), (await _bob.HistoryAsync()).Select(m => m.Id));
    }

    [Fact]
    public async Task Session_ends_when_the_channel_closes()
    {
        var link = Connect();
        link.AliceChannel.Close();

        await link.AliceSession.WaitAsync(TimeSpan.FromSeconds(5));
        await link.BobSession.WaitAsync(TimeSpan.FromSeconds(5));
        await link.DisposeAsync();
    }

    private Link Connect(Func<PeerEvent, bool>? aliceFilter = null, Func<PeerEvent, bool>? bobFilter = null)
    {
        var (a, b) = FakePeerChannel.CreatePair();
        a.Filter = aliceFilter ?? (_ => true);
        b.Filter = bobFilter ?? (_ => true);
        var cts = new CancellationTokenSource();
        return new Link(a, _alice.Run(a, cts.Token), _bob.Run(b, cts.Token), cts);
    }

    private sealed record Link(FakePeerChannel AliceChannel, Task AliceSession, Task BobSession, CancellationTokenSource Cancellation) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            AliceChannel.Close();
            await Cancellation.CancelAsync();
            await Task.WhenAll(AliceSession, BobSession).WaitAsync(TimeSpan.FromSeconds(5));
            Cancellation.Dispose();
        }
    }

    private sealed class Device : IAsyncDisposable
    {
        private Device(TestStore store, Contact contact, Conversation conversation)
        {
            Store = store;
            Contact = contact;
            Conversation = conversation;
        }

        public TestStore Store { get; private set; }

        public Contact Contact { get; }

        public Conversation Conversation { get; }

        public static async Task<Device> CreateAsync(string peerName)
        {
            var store = await TestStore.CreateAsync();
            var (contact, conversation) = await store.AddContactAsync(peerName);
            return new Device(store, contact, conversation);
        }

        public Task<Message> SendAsync(string body) =>
            new SendMessage(Store.Contacts, Store.Conversations, Store.Messages, Store.Outbox, Store.Events, TimeProvider.System)
                .ExecuteAsync(Contact.Id, body);

        public Task<IReadOnlyList<Message>> HistoryAsync(int limit = 100) => Store.Messages.ListRecentAsync(Conversation.Id, limit);

        public async Task RestartAsync() => Store = await Store.ReopenAsync();

        public Task WaitDeliveredAsync(MessageId id) =>
            Eventually.TrueAsync(async () => (await Store.Messages.GetAsync(Conversation.Id, id))?.Status == MessageStatus.Delivered, $"message {id} is delivered");

        public async Task Run(IPeerChannel channel, CancellationToken cancellationToken)
        {
            var session = new ConversationSyncSession(Store.Messages, Store.Outbox, Store.Events, TimeProvider.System, FastRetransmission);
            try
            {
                await session.RunAsync(Conversation, channel, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
