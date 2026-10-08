using System.Text;

namespace Murmur.Domain.Model;

public static class MessageRules
{
    /// <summary>Must equal the protocol's maximum body size.</summary>
    public const int MaxBodyBytes = 16 * 1024;

    /// <summary>
    /// Bound on how far a peer may move our logical clock in one message. Protects against a
    /// misbehaving peer pushing the clock towards overflow; honest peers never get close.
    /// </summary>
    public const long MaxLamportJump = 1_000_000;

    public static void ValidateBody(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new MurmurException(MurmurErrorCode.InvalidMessage, "Message is empty.");
        }

        if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            throw new MurmurException(MurmurErrorCode.MessageTooLarge, "Message is too large.");
        }
    }

    /// <summary>Current time at the precision stored and transmitted (milliseconds).</summary>
    public static DateTimeOffset Now(TimeProvider time) =>
        DateTimeOffset.FromUnixTimeMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());

    /// <summary>Lamport receive rule with a sanity bound: never move more than <see cref="MaxLamportJump"/> ahead.</summary>
    public static long ClampIncomingLamport(long incoming, long localClock) =>
        Math.Clamp(incoming, 0, localClock + MaxLamportJump);
}
