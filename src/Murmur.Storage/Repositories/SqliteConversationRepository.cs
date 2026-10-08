using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Storage.Database;
using Microsoft.Data.Sqlite;
using static Murmur.Storage.Database.SqlMapping;

namespace Murmur.Storage.Repositories;

public sealed class SqliteConversationRepository(SqliteDatabase database) : IConversationRepository
{
    public Task<Conversation> GetOrCreateForContactAsync(ContactId contactId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using (var insert = Command(c, "INSERT INTO conversations (id, contact_id, lamport, created_at) VALUES ($id, $contact, 0, $now) ON CONFLICT (contact_id) DO NOTHING", tx))
            {
                insert.With("$id", ToBlob(ConversationId.New().Value)).With("$contact", ToBlob(contactId.Value)).With("$now", ToUnixMs(now));
                try
                {
                    insert.ExecuteNonQuery();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
                {
                    throw new MurmurException(MurmurErrorCode.ContactNotFound, "Contact not found.", ex);
                }
            }

            using var select = Command(c, "SELECT id, contact_id, lamport, created_at FROM conversations WHERE contact_id = $contact", tx)
                .With("$contact", ToBlob(contactId.Value));
            using var reader = select.ExecuteReader();
            reader.Read();
            return Map(reader);
        }, cancellationToken);

    public Task<Conversation?> GetAsync(ConversationId id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c =>
        {
            using var command = Command(c, "SELECT id, contact_id, lamport, created_at FROM conversations WHERE id = $id").With("$id", ToBlob(id.Value));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }, cancellationToken);

    public Task<IReadOnlyList<ConversationSummary>> ListSummariesAsync(CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<ConversationSummary>>(c =>
        {
            const string sql = """
                SELECT conv.id, ct.id, ct.display_name, last.body, last.created_at,
                       (SELECT count(*) FROM messages p WHERE p.conversation_id = conv.id AND p.direction = 0 AND p.status IN (0, 1))
                FROM contacts ct
                JOIN conversations conv ON conv.contact_id = ct.id
                LEFT JOIN messages last ON last.rowid = (
                    SELECT m.rowid FROM messages m WHERE m.conversation_id = conv.id
                    ORDER BY m.lamport DESC, m.created_at DESC, m.id DESC LIMIT 1)
                ORDER BY coalesce(last.created_at, conv.created_at) DESC
                """;
            using var command = Command(c, sql);
            using var reader = command.ExecuteReader();
            var result = new List<ConversationSummary>();
            while (reader.Read())
            {
                result.Add(new ConversationSummary(
                    new ConversationId(GuidFrom(reader, 0)),
                    new ContactId(GuidFrom(reader, 1)),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : Preview(reader.GetString(3)),
                    NullableFromUnixMs(reader, 4),
                    reader.GetInt32(5)));
            }

            return result;
        }, cancellationToken);

    public Task ClearAsync(ConversationId id, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, "DELETE FROM messages WHERE conversation_id = $id", tx).With("$id", ToBlob(id.Value));
            return command.ExecuteNonQuery();
        }, cancellationToken);

    private static string Preview(string body) => body.Length <= 80 ? body : body[..80] + "…";

    private static Conversation Map(SqliteDataReader reader) => new(
        new ConversationId(GuidFrom(reader, 0)),
        new ContactId(GuidFrom(reader, 1)),
        reader.GetInt64(2),
        FromUnixMs(reader.GetInt64(3)));
}
