using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Storage.Database;
using Microsoft.Data.Sqlite;
using static Murmur.Storage.Database.SqlMapping;

namespace Murmur.Storage.Repositories;

public sealed class SqliteContactRepository(SqliteDatabase database) : IContactRepository
{
    private const string Columns = "id, identity_key, static_key, display_name, verification, is_blocked, created_at";

    public Task<Contact?> GetAsync(ContactId id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c => ReadSingle(Command(c, $"SELECT {Columns} FROM contacts WHERE id = $id").With("$id", ToBlob(id.Value))), cancellationToken);

    public Task<Contact?> FindByIdentityKeyAsync(PublicKey identityKey, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c => ReadSingle(Command(c, $"SELECT {Columns} FROM contacts WHERE identity_key = $key").With("$key", identityKey.ToArray())), cancellationToken);

    public Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<Contact>>(c =>
        {
            using var command = Command(c, $"SELECT {Columns} FROM contacts ORDER BY display_name COLLATE NOCASE, created_at");
            using var reader = command.ExecuteReader();
            var result = new List<Contact>();
            while (reader.Read())
            {
                result.Add(Map(reader));
            }

            return result;
        }, cancellationToken);

    public Task AddAsync(Contact contact, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, $"INSERT INTO contacts ({Columns}) VALUES ($id, $ik, $sk, $name, $ver, $blocked, $created)", tx);
            Bind(command, contact);
            try
            {
                return command.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new MurmurException(MurmurErrorCode.AlreadyPaired, "Contact already exists.", ex);
            }
        }, cancellationToken);

    public Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(
                c,
                "UPDATE contacts SET identity_key = $ik, static_key = $sk, display_name = $name, verification = $ver, is_blocked = $blocked, created_at = $created WHERE id = $id",
                tx);
            Bind(command, contact);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.");
            }

            return 1;
        }, cancellationToken);

    public Task DeleteAsync(ContactId id, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, "DELETE FROM contacts WHERE id = $id", tx).With("$id", ToBlob(id.Value));
            return command.ExecuteNonQuery();
        }, cancellationToken);

    private static void Bind(SqliteCommand command, Contact contact) =>
        command
            .With("$id", ToBlob(contact.Id.Value))
            .With("$ik", contact.Identity.IdentityKey.ToArray())
            .With("$sk", contact.Identity.StaticKey.ToArray())
            .With("$name", contact.DisplayName)
            .With("$ver", (int)contact.Verification)
            .With("$blocked", contact.IsBlocked ? 1 : 0)
            .With("$created", ToUnixMs(contact.CreatedAt));

    private static Contact? ReadSingle(SqliteCommand command)
    {
        using (command)
        {
            using var reader = command.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }
    }

    private static Contact Map(SqliteDataReader reader) => new(
        new ContactId(GuidFrom(reader, 0)),
        new PeerIdentity(KeyFrom(reader, 1), KeyFrom(reader, 2)),
        reader.GetString(3),
        (VerificationState)reader.GetInt32(4),
        reader.GetInt32(5) != 0,
        FromUnixMs(reader.GetInt64(6)));
}
