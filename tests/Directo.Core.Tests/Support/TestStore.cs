using System.Security.Cryptography;
using Directo.Domain.Common;
using Directo.Domain.Model;
using Directo.Domain.Ports;
using Directo.Storage.Database;
using Directo.Storage.Repositories;

namespace Directo.Core.Tests.Support;

/// <summary>A real encrypted SQLite database in a temporary file, with all repositories.</summary>
public sealed class TestStore : IAsyncDisposable
{
    private TestStore(string path, byte[] key, SqliteDatabase database)
    {
        Path = path;
        Key = key;
        Database = database;
        Contacts = new SqliteContactRepository(database);
        Conversations = new SqliteConversationRepository(database);
        Messages = new SqliteMessageRepository(database);
        Invites = new SqlitePendingInviteRepository(database);
        Settings = new SqliteSettingsRepository(database);
    }

    public string Path { get; }

    public byte[] Key { get; }

    public SqliteDatabase Database { get; }

    public SqliteContactRepository Contacts { get; }

    public SqliteConversationRepository Conversations { get; }

    public SqliteMessageRepository Messages { get; }

    public SqlitePendingInviteRepository Invites { get; }

    public SqliteSettingsRepository Settings { get; }

    public ChatEvents Events { get; } = new();

    public OutboxSignal Outbox { get; } = new();

    public static async Task<TestStore> CreateAsync(string? path = null, byte[]? key = null)
    {
        path ??= System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"directo-test-{Guid.NewGuid():N}.db");
        key ??= RandomNumberGenerator.GetBytes(32);
        return new TestStore(path, key, await SqliteDatabase.OpenAsync(path, key));
    }

    /// <summary>Closes and reopens the same file, simulating an app restart.</summary>
    public async Task<TestStore> ReopenAsync()
    {
        await Database.DisposeAsync();
        return await CreateAsync(Path, Key);
    }

    public async Task<(Contact Contact, Conversation Conversation)> AddContactAsync(string name = "Peer")
    {
        var contact = new Contact(
            ContactId.New(),
            new PeerIdentity(new PublicKey(RandomNumberGenerator.GetBytes(32)), new PublicKey(RandomNumberGenerator.GetBytes(32))),
            name,
            VerificationState.Unverified,
            IsBlocked: false,
            DateTimeOffset.UtcNow);
        await Contacts.AddAsync(contact);
        var conversation = await Conversations.GetOrCreateForContactAsync(contact.Id, DateTimeOffset.UtcNow);
        return (contact, conversation);
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm" })
        {
            File.Delete(file);
        }
    }
}
