namespace Murmur.Domain.Common;

/// <summary>Auto-reset async signal: <see cref="Set"/> before or during a wait releases exactly one waiter; signals coalesce.</summary>
public sealed class AsyncSignal
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _current = NewSource();
    private bool _isSet;

    public void Set()
    {
        TaskCompletionSource toComplete;
        lock (_gate)
        {
            if (_isSet)
            {
                return;
            }

            _isSet = true;
            toComplete = _current;
        }

        toComplete.TrySetResult();
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        Task task;
        lock (_gate)
        {
            if (_isSet)
            {
                _isSet = false;
                _current = NewSource();
                return;
            }

            task = _current.Task;
        }

        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_isSet)
            {
                _isSet = false;
                _current = NewSource();
            }
        }
    }

    private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
