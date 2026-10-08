namespace Murmur.Protocol;

public enum ProtocolErrorCode
{
    Malformed,
    TooLarge,
    UnsupportedVersion,
    InvalidSignature,
    Expired,
}

/// <summary>Raised when data received from the network does not satisfy the protocol.</summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(ProtocolErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public ProtocolErrorCode Code { get; }

    internal static ProtocolException Malformed(string message, Exception? inner = null) =>
        new(ProtocolErrorCode.Malformed, message, inner);
}
