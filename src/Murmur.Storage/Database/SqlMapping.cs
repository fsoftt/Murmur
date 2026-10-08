using Murmur.Domain.Model;
using Microsoft.Data.Sqlite;

namespace Murmur.Storage.Database;

internal static class SqlMapping
{
    public static byte[] ToBlob(Guid id) => id.ToByteArray(bigEndian: true);

    public static Guid GuidFrom(SqliteDataReader reader, int ordinal) => new(reader.GetFieldValue<byte[]>(ordinal), bigEndian: true);

    public static long ToUnixMs(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    public static DateTimeOffset FromUnixMs(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);

    public static DateTimeOffset? NullableFromUnixMs(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : FromUnixMs(reader.GetInt64(ordinal));

    public static object DbValue(DateTimeOffset? value) => value is null ? DBNull.Value : ToUnixMs(value.Value);

    public static SqliteCommand Command(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    public static SqliteCommand With(this SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static PublicKey KeyFrom(SqliteDataReader reader, int ordinal) => new(reader.GetFieldValue<byte[]>(ordinal));
}
