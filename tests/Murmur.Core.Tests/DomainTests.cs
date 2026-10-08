using Murmur.Core.Tests.Support;
using Murmur.Domain.Common;
using Murmur.Domain.Connections;
using Murmur.Domain.Model;
using Murmur.Domain.UseCases;

namespace Murmur.Core.Tests;

public class DomainTests
{
    [Fact]
    public void State_machine_follows_the_happy_path()
    {
        var machine = new PeerConnectionStateMachine();
        var seen = new List<PeerConnectionState>();
        machine.StateChanged += (_, s) => seen.Add(s);

        Assert.True(machine.Fire(PeerConnectionTrigger.Start));
        Assert.True(machine.Fire(PeerConnectionTrigger.PeerSeen));
        Assert.True(machine.Fire(PeerConnectionTrigger.Established));
        Assert.True(machine.Fire(PeerConnectionTrigger.Lost));
        Assert.True(machine.Fire(PeerConnectionTrigger.RetryDue));
        Assert.True(machine.Fire(PeerConnectionTrigger.Failed));
        Assert.True(machine.Fire(PeerConnectionTrigger.PeerGone));
        Assert.True(machine.Fire(PeerConnectionTrigger.Stop));

        Assert.Equal(
            [
                PeerConnectionState.Discovering, PeerConnectionState.Negotiating, PeerConnectionState.Connected,
                PeerConnectionState.Reconnecting, PeerConnectionState.Negotiating, PeerConnectionState.Reconnecting,
                PeerConnectionState.Discovering, PeerConnectionState.Disconnected,
            ],
            seen);
    }

    [Theory]
    [InlineData(PeerConnectionTrigger.Established)]
    [InlineData(PeerConnectionTrigger.Lost)]
    [InlineData(PeerConnectionTrigger.PeerSeen)]
    public void Impossible_transitions_are_refused(PeerConnectionTrigger trigger)
    {
        var machine = new PeerConnectionStateMachine();

        Assert.False(machine.Fire(trigger));
        Assert.Equal(PeerConnectionState.Disconnected, machine.State);
    }

    [Fact]
    public void Connected_session_cannot_be_renegotiated_without_being_lost_first()
    {
        var machine = new PeerConnectionStateMachine();
        machine.Fire(PeerConnectionTrigger.Start);
        machine.Fire(PeerConnectionTrigger.PeerSeen);
        machine.Fire(PeerConnectionTrigger.Established);

        Assert.False(machine.Fire(PeerConnectionTrigger.PeerSeen));
        Assert.False(machine.Fire(PeerConnectionTrigger.PeerGone));
        Assert.Equal(PeerConnectionState.Connected, machine.State);
    }

    [Theory]
    [InlineData(0, 0.0, 500)]
    [InlineData(0, 0.999, 1000)]
    [InlineData(3, 0.999, 8000)]
    [InlineData(20, 0.999, 60000)]
    public void Backoff_grows_exponentially_and_is_capped(int attempt, double random, int maxExpectedMs)
    {
        var backoff = new Backoff(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        var delay = backoff.Delay(attempt, random);

        Assert.InRange(delay.TotalMilliseconds, maxExpectedMs / 2.0 - 1, maxExpectedMs);
    }

    [Fact]
    public void Token_bucket_allows_a_burst_then_asks_to_wait()
    {
        var bucket = new TokenBucket(ratePerSecond: 10, capacity: 3, TimeProvider.System);

        Assert.Equal(TimeSpan.Zero, bucket.Reserve());
        Assert.Equal(TimeSpan.Zero, bucket.Reserve());
        Assert.Equal(TimeSpan.Zero, bucket.Reserve());
        Assert.InRange(bucket.Reserve().TotalMilliseconds, 50, 100);
        Assert.InRange(bucket.Reserve().TotalMilliseconds, 150, 200);
    }

    [Fact]
    public async Task Signal_set_before_wait_is_not_lost()
    {
        var signal = new AsyncSignal();
        signal.Set();
        signal.Set();

        await signal.WaitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        var second = signal.WaitAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);
        signal.Set();
        await second.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_messages_are_rejected(string body)
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, _) = await store.AddContactAsync();

        var ex = await Assert.ThrowsAsync<MurmurException>(() => NewSend(store).ExecuteAsync(contact.Id, body));
        Assert.Equal(MurmurErrorCode.InvalidMessage, ex.Code);
    }

    [Fact]
    public async Task Oversized_messages_are_rejected()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, _) = await store.AddContactAsync();

        var ex = await Assert.ThrowsAsync<MurmurException>(() => NewSend(store).ExecuteAsync(contact.Id, new string('x', MessageRules.MaxBodyBytes + 1)));
        Assert.Equal(MurmurErrorCode.MessageTooLarge, ex.Code);
    }

    [Fact]
    public async Task Sending_to_a_blocked_contact_is_refused()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, _) = await store.AddContactAsync();
        await new ManageContacts(store.Contacts, store.Conversations, store.Events).SetBlockedAsync(contact.Id, true);

        var ex = await Assert.ThrowsAsync<MurmurException>(() => NewSend(store).ExecuteAsync(contact.Id, "hi"));
        Assert.Equal(MurmurErrorCode.ContactBlocked, ex.Code);
    }

    [Fact]
    public async Task Sent_message_is_stored_pending_and_announced()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, conversation) = await store.AddContactAsync();
        Message? announced = null;
        store.Events.MessageStored += (_, m) => announced = m;

        var message = await NewSend(store).ExecuteAsync(contact.Id, "hola");

        Assert.Equal(MessageStatus.Pending, message.Status);
        Assert.Equal(message, announced);
        Assert.Equal([message], await store.Messages.ListOutboxAsync(conversation.Id));
    }

    private static SendMessage NewSend(TestStore store) =>
        new(store.Contacts, store.Conversations, store.Messages, store.Outbox, store.Events, TimeProvider.System);
}
