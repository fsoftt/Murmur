using System.Text;
using Directo.Core.Tests.Support;
using Directo.Domain.Model;
using Directo.Storage.Database;

namespace Directo.Core.Tests;

public class StorageTests
{
    [Fact]
    public async Task Database_file_does_not_contain_plaintext()
    {
        var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync("Secreto");
        const string marker = "very-distinctive-plaintext-marker";
        await store.Messages.AppendOutgoingAsync(new OutgoingMessageDraft(MessageId.New(), conversation.Id, marker, DateTimeOffset.UtcNow));
        await using var reopened = await store.ReopenAsync();
        await reopened.Database.DisposeAsync();

        var bytes = string.Join('\0', new[] { store.Path, store.Path + "-wal" }.Where(File.Exists).Select(p => Encoding.Latin1.GetString(File.ReadAllBytes(p))));

        Assert.DoesNotContain(marker, bytes, StringComparison.Ordinal);
        Assert.DoesNotContain("Secreto", bytes, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLite format 3", bytes, StringComparison.Ordinal);
        File.Delete(store.Path);
    }

    [Fact]
    public async Task Wrong_key_is_refused()
    {
        var store = await TestStore.CreateAsync();
        await store.Database.DisposeAsync();

        var ex = await Assert.ThrowsAsync<DirectoException>(() => SqliteDatabase.OpenAsync(store.Path, new byte[32]));

        Assert.Equal(DirectoErrorCode.StorageFailure, ex.Code);
        File.Delete(store.Path);
    }

    [Fact]
    public async Task Reopening_keeps_data_and_does_not_rerun_migrations()
    {
        var store = await TestStore.CreateAsync();
        var (contact, _) = await store.AddContactAsync("Ana");

        await using var reopened = await store.ReopenAsync();

        Assert.Equal("Ana", (await reopened.Contacts.GetAsync(contact.Id))!.DisplayName);
    }

    [Fact]
    public async Task Duplicate_identity_is_reported_as_already_paired()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, _) = await store.AddContactAsync();

        var ex = await Assert.ThrowsAsync<DirectoException>(() => store.Contacts.AddAsync(contact with { Id = ContactId.New() }));

        Assert.Equal(DirectoErrorCode.AlreadyPaired, ex.Code);
    }

    [Fact]
    public async Task Get_or_create_conversation_is_idempotent()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, conversation) = await store.AddContactAsync();

        var again = await store.Conversations.GetOrCreateForContactAsync(contact.Id, DateTimeOffset.UtcNow);

        Assert.Equal(conversation.Id, again.Id);
    }

    [Fact]
    public async Task Outgoing_messages_get_increasing_lamport_times()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync();

        var first = await store.Messages.AppendOutgoingAsync(Draft(conversation, "1"));
        var second = await store.Messages.AppendOutgoingAsync(Draft(conversation, "2"));

        Assert.Equal(1, first.Lamport);
        Assert.Equal(2, second.Lamport);
        Assert.Equal(MessageStatus.Pending, second.Status);
    }

    [Fact]
    public async Task Incoming_messages_are_deduplicated_and_advance_the_clock()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync();
        var incoming = Incoming(conversation, lamport: 10);

        Assert.True(await store.Messages.TryAppendIncomingAsync(incoming));
        Assert.False(await store.Messages.TryAppendIncomingAsync(incoming));

        var reply = await store.Messages.AppendOutgoingAsync(Draft(conversation, "reply"));
        Assert.Equal(11, reply.Lamport);
        Assert.Single(await store.Messages.ListRecentAsync(conversation.Id, 10), m => m.Direction == MessageDirection.Incoming);
    }

    [Fact]
    public async Task Hostile_lamport_values_are_clamped()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync();

        await store.Messages.TryAppendIncomingAsync(Incoming(conversation, lamport: long.MaxValue));
        var reply = await store.Messages.AppendOutgoingAsync(Draft(conversation, "still works"));

        Assert.Equal(MessageRules.MaxLamportJump + 1, reply.Lamport);
    }

    [Fact]
    public async Task Peer_cannot_mark_an_incoming_message_as_delivered()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync();
        var incoming = Incoming(conversation, 1);
        await store.Messages.TryAppendIncomingAsync(incoming);

        Assert.False(await store.Messages.MarkDeliveredAsync(conversation.Id, incoming.Id, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Ack_for_another_conversation_is_ignored()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, mine) = await store.AddContactAsync("A");
        var (_, other) = await store.AddContactAsync("B");
        var message = await store.Messages.AppendOutgoingAsync(Draft(mine, "x"));

        Assert.False(await store.Messages.MarkDeliveredAsync(other.Id, message.Id, DateTimeOffset.UtcNow));
        Assert.True(await store.Messages.MarkDeliveredAsync(mine.Id, message.Id, DateTimeOffset.UtcNow));
        Assert.Empty(await store.Messages.ListOutboxAsync(mine.Id));
    }

    [Fact]
    public async Task Deleting_a_contact_removes_its_history()
    {
        await using var store = await TestStore.CreateAsync();
        var (contact, conversation) = await store.AddContactAsync();
        await store.Messages.AppendOutgoingAsync(Draft(conversation, "bye"));

        await store.Contacts.DeleteAsync(contact.Id);

        Assert.Null(await store.Conversations.GetAsync(conversation.Id));
        Assert.Empty(await store.Messages.ListRecentAsync(conversation.Id, 10));
    }

    [Fact]
    public async Task Summaries_show_last_message_and_pending_count()
    {
        await using var store = await TestStore.CreateAsync();
        var (_, conversation) = await store.AddContactAsync("Ana");
        await store.Messages.AppendOutgoingAsync(Draft(conversation, "first"));
        await store.Messages.AppendOutgoingAsync(Draft(conversation, "second"));

        var summary = Assert.Single(await store.Conversations.ListSummariesAsync());

        Assert.Equal("Ana", summary.DisplayName);
        Assert.Equal("second", summary.LastMessagePreview);
        Assert.Equal(2, summary.PendingCount);
    }

    [Fact]
    public async Task Invites_are_single_use()
    {
        await using var store = await TestStore.CreateAsync();
        var token = new byte[16];
        await store.Invites.AddAsync(new Domain.Ports.PendingInvite(token, DateTimeOffset.UtcNow.AddMinutes(5), null));
        var first = new PublicKey(new byte[32]);

        await store.Invites.MarkConsumedAsync(token, first);
        await store.Invites.MarkConsumedAsync(token, new PublicKey(Enumerable.Repeat((byte)1, 32).ToArray()));

        Assert.Equal(first, (await store.Invites.FindAsync(token))!.ConsumedBy);
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        await using var store = await TestStore.CreateAsync();

        await store.Settings.SetAsync("profile", "Ana");
        Assert.Equal("Ana", await store.Settings.GetAsync("profile"));
        await store.Settings.SetAsync("profile", null);
        Assert.Null(await store.Settings.GetAsync("profile"));
    }

    private static OutgoingMessageDraft Draft(Conversation conversation, string body) =>
        new(MessageId.New(), conversation.Id, body, DateTimeOffset.UtcNow);

    private static Message Incoming(Conversation conversation, long lamport) =>
        new(MessageId.New(), conversation.Id, MessageDirection.Incoming, "hi", MessageStatus.Received, lamport, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
