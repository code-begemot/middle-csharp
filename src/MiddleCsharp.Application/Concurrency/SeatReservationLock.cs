namespace MiddleCsharp.Application.Concurrency;

internal sealed class SeatReservationLock : ISeatReservationLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();
}
