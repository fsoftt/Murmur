using System.Formats.Cbor;
using System.Text;

namespace Directo.Protocol.Serialization;

/// <summary>
/// Helpers for the protocol's CBOR convention: every structure is a definite-length map
/// with small unsigned integer keys. Unknown keys are skipped so that newer peers can add
/// fields without breaking older ones.
/// </summary>
internal static class CborMap
{
    public static CborWriter CreateWriter() => new(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: false);

    public static CborReader CreateReader(ReadOnlyMemory<byte> data, int maxBytes)
    {
        if (data.Length > maxBytes)
        {
            throw new ProtocolException(ProtocolErrorCode.TooLarge, $"Payload of {data.Length} bytes exceeds limit of {maxBytes}.");
        }

        return new CborReader(data, CborConformanceMode.Strict);
    }

    /// <summary>Reads a map, invoking <paramref name="onField"/> for each key. The callback returns false for unknown keys, which are skipped.</summary>
    public static void ReadMap(CborReader reader, Func<uint, CborReader, bool> onField, int maxFields = 32)
    {
        try
        {
            int? count = reader.ReadStartMap();
            if (count is null or < 0 || count > maxFields)
            {
                throw ProtocolException.Malformed("Map must have a definite, bounded length.");
            }

            for (var i = 0; i < count; i++)
            {
                if (reader.PeekState() != CborReaderState.UnsignedInteger)
                {
                    throw ProtocolException.Malformed("Map keys must be unsigned integers.");
                }

                var key = reader.ReadUInt32();
                if (!onField(key, reader))
                {
                    reader.SkipValue();
                }
            }

            reader.ReadEndMap();
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException or OverflowException or FormatException)
        {
            throw ProtocolException.Malformed("Invalid CBOR structure.", ex);
        }
    }

    public static void EnsureFullyConsumed(CborReader reader)
    {
        if (reader.BytesRemaining != 0)
        {
            throw ProtocolException.Malformed("Trailing bytes after structure.");
        }
    }

    public static byte[] ReadFixedBytes(CborReader reader, int size, string field)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != size)
        {
            throw ProtocolException.Malformed($"Field '{field}' must be {size} bytes.");
        }

        return bytes;
    }

    public static string ReadBoundedText(CborReader reader, int maxUtf8Bytes, string field)
    {
        var text = reader.ReadTextString();
        if (System.Text.Encoding.UTF8.GetByteCount(text) > maxUtf8Bytes)
        {
            throw new ProtocolException(ProtocolErrorCode.TooLarge, $"Field '{field}' exceeds {maxUtf8Bytes} bytes.");
        }

        return text;
    }

    public static Guid ReadGuid(CborReader reader, string field) =>
        new(ReadFixedBytes(reader, ProtocolConstants.MessageIdSize, field), bigEndian: true);

    public static void WriteGuid(CborWriter writer, Guid value)
    {
        Span<byte> buffer = stackalloc byte[ProtocolConstants.MessageIdSize];
        value.TryWriteBytes(buffer, bigEndian: true, out _);
        writer.WriteByteString(buffer);
    }

    public static T Required<T>(T? value, string field)
        where T : class =>
        value ?? throw ProtocolException.Malformed($"Missing required field '{field}'.");

    public static T Required<T>(T? value, string field)
        where T : struct =>
        value ?? throw ProtocolException.Malformed($"Missing required field '{field}'.");

    public static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
