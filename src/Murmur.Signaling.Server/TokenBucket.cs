namespace Murmur.Signaling.Server;

/// <summary>Per-connection rate limiter. Not thread-safe; used from the connection's receive loop only.</summary>
internal sealed class TokenBucket(double ratePerSecond, int capacity, TimeProvider time)
{
    private double _tokens = capacity;
    private long _last = time.GetTimestamp();

    public bool TryTake()
    {
        var now = time.GetTimestamp();
        _tokens = Math.Min(capacity, _tokens + time.GetElapsedTime(_last, now).TotalSeconds * ratePerSecond);
        _last = now;
        if (_tokens < 1)
        {
            return false;
        }

        _tokens -= 1;
        return true;
    }
}
