using Murmur.Domain.Model;
using Murmur.Domain.Ports;
using Murmur.Storage.Database;
using static Murmur.Storage.Database.SqlMapping;

namespace Murmur.Storage.Repositories;

public sealed class SqlitePendingInviteRepository(SqliteDatabase database) : IPendingInviteRepository
{
    public Task AddAsync(PendingInvite invite, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, "INSERT INTO pending_invites (token, expires_at, consumed_by) VALUES ($token, $expires, $by)", tx)
                .With("$token", invite.Token)
                .With("$expires", ToUnixMs(invite.ExpiresAt))
                .With("$by", invite.ConsumedBy?.ToArray());
            return command.ExecuteNonQuery();
        }, cancellationToken);

    public Task<PendingInvite?> FindAsync(byte[] token, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c =>
        {
            using var command = Command(c, "SELECT token, expires_at, consumed_by FROM pending_invites WHERE token = $token").With("$token", token);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new PendingInvite(
                reader.GetFieldValue<byte[]>(0),
                FromUnixMs(reader.GetInt64(1)),
                reader.IsDBNull(2) ? null : KeyFrom(reader, 2));
        }, cancellationToken);

    public Task MarkConsumedAsync(byte[] token, PublicKey consumedBy, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, "UPDATE pending_invites SET consumed_by = $by WHERE token = $token AND consumed_by IS NULL", tx)
                .With("$token", token)
                .With("$by", consumedBy.ToArray());
            return command.ExecuteNonQuery();
        }, cancellationToken);

    public Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = Command(c, "DELETE FROM pending_invites WHERE expires_at < $now", tx).With("$now", ToUnixMs(now));
            return command.ExecuteNonQuery();
        }, cancellationToken);
}

public sealed class SqliteSettingsRepository(SqliteDatabase database) : ISettingsRepository
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        database.ReadAsync(c =>
        {
            using var command = Command(c, "SELECT value FROM settings WHERE key = $key").With("$key", key);
            return command.ExecuteScalar() as string;
        }, cancellationToken);

    public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default) =>
        database.WriteAsync((c, tx) =>
        {
            using var command = value is null
                ? Command(c, "DELETE FROM settings WHERE key = $key", tx).With("$key", key)
                : Command(c, "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT (key) DO UPDATE SET value = excluded.value", tx)
                    .With("$key", key)
                    .With("$value", value);
            return command.ExecuteNonQuery();
        }, cancellationToken);
}
