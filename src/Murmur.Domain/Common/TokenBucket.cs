namespace Murmur.Domain.Common;

/// <summary>
/// Token bucket that answers "how long must I wait?" instead of refusing, so callers can apply
/// backpressure (slow down) rather than drop work. Thread-safe.
/// </summary>
public sealed class TokenBucket
{
    private readonly double _ratePerSecond;
    private readonly int _capacity;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private double _tokens;
    private long _last;

    public TokenBucket(double ratePerSecond, int capacity, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ratePerSecond);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _ratePerSecond = ratePerSecond;
        _capacity = capacity;
        _time = time;
        _tokens = capacity;
        _last = time.GetTimestamp();
    }

    /// <summary>Takes one token, possibly going into debt, and returns how long to wait before acting.</summary>
    public TimeSpan Reserve()
    {
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            _tokens = Math.Min(_capacity, _tokens + _time.GetElapsedTime(_last, now).TotalSeconds * _ratePerSecond);
            _last = now;
            _tokens -= 1;
            return _tokens >= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(-_tokens / _ratePerSecond);
        }
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        var delay = Reserve();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
        }
    }
}
