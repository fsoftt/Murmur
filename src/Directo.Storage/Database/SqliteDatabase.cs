using Directo.Domain.Model;
using Microsoft.Data.Sqlite;

namespace Directo.Storage.Database;

/// <summary>
/// Encrypted local database (SQLCipher). A single connection is shared and every operation is
/// serialized, which keeps transactions simple and avoids SQLITE_BUSY on mobile devices where
/// write throughput is tiny. The raw 256-bit key comes from the platform secret store.
/// </summary>
public sealed class SqliteDatabase : IAsyncDisposable
{
    private static int s_initialized;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SqliteConnection _connection;
    private int _disposed;

    private SqliteDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Opens (or creates) the database, verifies the key and applies pending migrations.</summary>
    /// <param name="path">Database file path, or ":memory:" for tests.</param>
    /// <param name="key">32-byte raw SQLCipher key. Not retained.</param>
    public static async Task<SqliteDatabase> OpenAsync(string path, byte[] key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32)
        {
            throw new ArgumentException("Database key must be 32 bytes.", nameof(key));
        }

        if (Interlocked.Exchange(ref s_initialized, 1) == 0)
        {
            SQLitePCL.Batteries_V2.Init();
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            ApplyKey(connection, key);
            EnsureEncryptionAvailable(connection);
            VerifyKey(connection);
            Execute(connection, "PRAGMA foreign_keys = ON;");
            Execute(connection, "PRAGMA journal_mode = WAL;");
            Execute(connection, "PRAGMA synchronous = FULL;");
            Execute(connection, "PRAGMA secure_delete = ON;");
            Migrations.Apply(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return new SqliteDatabase(connection);
    }

    public async Task<T> ReadAsync<T>(Func<SqliteConnection, T> operation, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // SQLite calls are synchronous; keep them off the caller's (often the UI) thread.
            return await Task.Run(() => operation(_connection), CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            throw new DirectoException(DirectoErrorCode.StorageFailure, "Local database operation failed.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs <paramref name="operation"/> inside a transaction that commits only if it returns normally.</summary>
    public Task<T> WriteAsync<T>(Func<SqliteConnection, SqliteTransaction, T> operation, CancellationToken cancellationToken = default) =>
        ReadAsync(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var result = operation(connection, transaction);
            transaction.Commit();
            return result;
        }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    internal static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void ApplyKey(SqliteConnection connection, byte[] key)
    {
        // Raw-key form skips SQLCipher's passphrase KDF; the key is already uniformly random.
        // PRAGMA statements cannot be parameterized; the value is hex we produced ourselves.
        Execute(connection, $"PRAGMA key = \"x'{Convert.ToHexString(key)}'\";");
    }

    private static void EnsureEncryptionAvailable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA cipher_version;";
        if (command.ExecuteScalar() is not string version || version.Length == 0)
        {
            throw new DirectoException(DirectoErrorCode.StorageFailure, "SQLCipher is not available; refusing to store data unencrypted.");
        }
    }

    private static void VerifyKey(SqliteConnection connection)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master;";
            command.ExecuteScalar();
        }
        catch (SqliteException ex)
        {
            throw new DirectoException(DirectoErrorCode.StorageFailure, "Database key is wrong or the file is corrupted.", ex);
        }
    }
}
