namespace MiddleCsharp.Tests;

internal sealed class DelayTrackingTimeProvider : TimeProvider
{
    private readonly int _expectedDelays;
    private readonly TaskCompletionSource _expectedDelaysStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeDelays;
    private int _maxActiveDelays;

    public DelayTrackingTimeProvider(int expectedDelays)
    {
        if (expectedDelays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedDelays));
        }

        _expectedDelays = expectedDelays;
    }

    public int MaxActiveDelays => Volatile.Read(ref _maxActiveDelays);

    public Task ExpectedDelaysStarted => _expectedDelaysStarted.Task;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        var activeDelays = Interlocked.Increment(ref _activeDelays);
        UpdateMaxActiveDelays(activeDelays);

        if (activeDelays >= _expectedDelays)
        {
            _expectedDelaysStarted.TrySetResult();
        }

        return new NonFiringTimer(this);
    }

    private void OnTimerDisposed()
    {
        Interlocked.Decrement(ref _activeDelays);
    }

    private void UpdateMaxActiveDelays(int activeDelays)
    {
        var currentMax = Volatile.Read(ref _maxActiveDelays);

        while (activeDelays > currentMax)
        {
            var observedMax = Interlocked.CompareExchange(
                ref _maxActiveDelays,
                activeDelays,
                currentMax);

            if (observedMax == currentMax)
            {
                return;
            }

            currentMax = observedMax;
        }
    }

    private sealed class NonFiringTimer : ITimer
    {
        private readonly DelayTrackingTimeProvider _owner;
        private int _disposed;

        public NonFiringTimer(DelayTrackingTimeProvider owner)
        {
            _owner = owner;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.OnTimerDisposed();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
