namespace Directo.Domain.Common;

/// <summary>Exponential backoff with jitter, used for reconnections and retransmissions.</summary>
public sealed record Backoff(TimeSpan Initial, TimeSpan Maximum, double JitterRatio = 0.5)
{
    /// <param name="attempt">Zero-based attempt number.</param>
    /// <param name="random">Value in [0, 1) used for jitter; injectable for tests.</param>
    public TimeSpan Delay(int attempt, double random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        var exponential = Initial.TotalMilliseconds * Math.Pow(2, Math.Min(attempt, 30));
        var capped = Math.Min(exponential, Maximum.TotalMilliseconds);
        var jittered = capped * (1 - JitterRatio + JitterRatio * random);
        return TimeSpan.FromMilliseconds(jittered);
    }
}
