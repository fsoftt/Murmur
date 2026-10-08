using Microsoft.Data.Sqlite;

namespace Murmur.Storage.Database;

/// <summary>
/// Ordered, append-only schema migrations tracked with <c>PRAGMA user_version</c>. Each runs in
/// its own transaction; never edit a released migration, add a new one instead.
/// </summary>
internal static class Migrations
{
    internal static readonly IReadOnlyList<string> Steps =
    [
        // 1: initial schema
        """
        CREATE TABLE contacts (
            id            BLOB    PRIMARY KEY CHECK (length(id) = 16),
            identity_key  BLOB    NOT NULL UNIQUE CHECK (length(identity_key) = 32),
            static_key    BLOB    NOT NULL CHECK (length(static_key) = 32),
            display_name  TEXT    NOT NULL,
            verification  INTEGER NOT NULL DEFAULT 0,
            is_blocked    INTEGER NOT NULL DEFAULT 0,
            created_at    INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE conversations (
            id          BLOB    PRIMARY KEY CHECK (length(id) = 16),
            contact_id  BLOB    NOT NULL UNIQUE REFERENCES contacts (id) ON DELETE CASCADE,
            lamport     INTEGER NOT NULL DEFAULT 0 CHECK (lamport >= 0),
            created_at  INTEGER NOT NULL
        ) STRICT;

        CREATE TABLE messages (
            conversation_id  BLOB    NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
            id               BLOB    NOT NULL CHECK (length(id) = 16),
            direction        INTEGER NOT NULL,
            body             TEXT    NOT NULL,
            status           INTEGER NOT NULL,
            lamport          INTEGER NOT NULL CHECK (lamport >= 0),
            created_at       INTEGER NOT NULL,
            received_at      INTEGER,
            delivered_at     INTEGER,
            PRIMARY KEY (conversation_id, id)
        ) STRICT;

        CREATE INDEX ix_messages_order ON messages (conversation_id, lamport, created_at, id);
        CREATE INDEX ix_messages_outbox ON messages (conversation_id, lamport) WHERE direction = 0 AND status IN (0, 1);

        CREATE TABLE pending_invites (
            token        BLOB    PRIMARY KEY CHECK (length(token) = 16),
            expires_at   INTEGER NOT NULL,
            consumed_by  BLOB
        ) STRICT;

        CREATE TABLE settings (
            key    TEXT PRIMARY KEY,
            value  TEXT
        ) STRICT;
        """,
    ];

    public static int LatestVersion => Steps.Count;

    public static void Apply(SqliteConnection connection)
    {
        var current = GetVersion(connection);
        if (current > LatestVersion)
        {
            throw new InvalidOperationException($"Database schema {current} is newer than this app ({LatestVersion}).");
        }

        for (var version = current + 1; version <= LatestVersion; version++)
        {
            using var transaction = connection.BeginTransaction();
            SqliteDatabase.Execute(connection, Steps[version - 1], transaction);
            SqliteDatabase.Execute(connection, $"PRAGMA user_version = {version};", transaction);
            transaction.Commit();
        }
    }

    public static int GetVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
