using Directo.Domain.Model;
using Directo.Domain.Ports;
using Directo.Storage.Database;
using Microsoft.Data.Sqlite;
using static Directo.Storage.Database.SqlMapping;

namespace Directo.Storage.Repositories;

public sealed class SqliteMessageRepository(SqliteDatabase database) : IMessageRepository
{
    private const string Columns = "id, conversation_id, direction, body, status, lamport, created_at, received_at, delivered_at";

    public Task<Message> AppendOutgoingAsync(OutgoingMessageDraft draft, CancellationToken cancellationToken = default)
    {
        MessageRules.ValidateBody(draft.Body);
        return database.WriteAsync((c, tx) =>
        {
            long lamport;
            using (var tick = Command(c, "UPDATE conversations SET lamport = lamport + 1 WHERE id = $conv RETURNING lamport", tx))
            {
                tick.With("$conv", ToBlob(draft.ConversationId.Value));
                lamport = tick.ExecuteScalar() is long value
                    ? value
                    : throw new DirectoException(DirectoErrorCode.ConversationNotFound, "Conversation not found.");
            }

            var message = new Message(draft.Id, draft.ConversationId, MessageDirection.Outgoing, draft.Body, MessageStatus.Pending, lamport, draft.CreatedAt);
            using var insert = Command(c, $"INSERT INTO messages ({Columns}) VALUES ($id, $conv, $dir, $body, $status, $lamport, $created, $received, $delivered)", tx);
            Bind(insert, message).ExecuteNonQuery();
            return message;
        }, cancellationToken);
    }

    public Task<bool> TryAppendIncomingAsync(Message message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Direction != MessageDirection.Incoming)
        {
            throw new ArgumentException("Message must be incoming.", nameof(message));
        }

        return database.WriteAsync((c, tx) =>
        {
            long localClock;
            using (var clock = Command(c, "SELECT lamport FROM conversations WHERE id = $conv", tx))
            {
                clock.With("$conv", ToBlob(message.ConversationId.Value));
                localClock = clock.ExecuteScalar() is long value
                    ? value
                    : throw new DirectoException(DirectoErrorCode.ConversationNotFound, "Conversation not found.");
            }

            var stored = message with { Lamport = MessageRules.ClampIncomingLamport(message.Lamport, localClock) };
            using (var insert = Command(c, $"INSERT INTO messages ({Columns}) VALUES ($id, $conv, $dir, $body, $status, $lamport, $created, $received, $delivered) ON CONFLICT DO NOTHING", tx))
            {
                if (Bind(insert, stored).ExecuteNonQuery() == 0)
                {
                    return false;
                }
            }

            using var advance = Command(c, "UPDATE conversations SET lamport = max(lamport, $lamport) WHERE id = $conv", tx)
                .With("$lamport", stored.Lamport)
                .With("$conv", ToBlob(message.ConversationId.Value));
            advance.ExecuteNonQuery();
            return true;
        }, cancellationToken);
    }

    public Task<Message?> GetAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c =>
        {
            using var command = Command(c, $"SELECT {Columns} FROM messages WHERE conversation_id = $conv AND id = $id")
                .With("$conv", ToBlob(conversationId.Value))
                .With("$id", ToBlob(id.Value));
            using var reader = command.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }, cancellationToken);

    public Task<IReadOnlyList<Message>> ListRecentAsync(ConversationId conversationId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return database.ReadAsync<IReadOnlyList<Message>>(c =>
        {
            using var command = Command(c, $"SELECT {Columns} FROM messages WHERE conversation_id = $conv ORDER BY lamport DESC, created_at DESC, id DESC LIMIT $limit")
                .With("$conv", ToBlob(conversationId.Value))
                .With("$limit", limit);
            var result = ReadAll(command);
            result.Reverse();
            return result;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<Message>> ListOutboxAsync(ConversationId conversationId, CancellationToken cancellationToken = default) =>
        database.ReadAsync<IReadOnlyList<Message>>(c =>
        {
            using var command = Command(c, $"SELECT {Columns} FROM messages WHERE conversation_id = $conv AND direction = 0 AND status IN (0, 1) ORDER BY lamport, created_at, id")
                .With("$conv", ToBlob(conversationId.Value));
            return ReadAll(command);
        }, cancellationToken);

    public Task<bool> MarkSentAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(conversationId, id, "status = 1", "status = 0", null, cancellationToken);

    public Task<bool> MarkDeliveredAsync(ConversationId conversationId, MessageId id, DateTimeOffset deliveredAt, CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(conversationId, id, "status = 2, delivered_at = $at", "status IN (0, 1)", deliveredAt, cancellationToken);

    public Task<bool> RequeueAsync(ConversationId conversationId, MessageId id, CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(conversationId, id, "status = 0", "status = 3", null, cancellationToken);

    private Task<bool> UpdateStatusAsync(ConversationId conversationId, MessageId id, string set, string expected, DateTimeOffset? at, CancellationToken cancellationToken) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, $"UPDATE messages SET {set} WHERE conversation_id = $conv AND id = $id AND direction = 0 AND {expected}", tx)
                .With("$conv", ToBlob(conversationId.Value))
                .With("$id", ToBlob(id.Value))
                .With("$at", DbValue(at));
            return command.ExecuteNonQuery() == 1;
        }, cancellationToken);

    private static SqliteCommand Bind(SqliteCommand command, Message message) =>
        command
            .With("$id", ToBlob(message.Id.Value))
            .With("$conv", ToBlob(message.ConversationId.Value))
            .With("$dir", (int)message.Direction)
            .With("$body", message.Body)
            .With("$status", (int)message.Status)
            .With("$lamport", message.Lamport)
            .With("$created", ToUnixMs(message.CreatedAt))
            .With("$received", DbValue(message.ReceivedAt))
            .With("$delivered", DbValue(message.DeliveredAt));

    private static List<Message> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var result = new List<Message>();
        while (reader.Read())
        {
            result.Add(Map(reader));
        }

        return result;
    }

    private static Message Map(SqliteDataReader reader) => new(
        new MessageId(GuidFrom(reader, 0)),
        new ConversationId(GuidFrom(reader, 1)),
        (MessageDirection)reader.GetInt32(2),
        reader.GetString(3),
        (MessageStatus)reader.GetInt32(4),
        reader.GetInt64(5),
        FromUnixMs(reader.GetInt64(6)),
        NullableFromUnixMs(reader, 7),
        NullableFromUnixMs(reader, 8));
}
