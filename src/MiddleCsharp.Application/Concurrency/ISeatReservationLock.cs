namespace MiddleCsharp.Application.Concurrency;

public interface ISeatReservationLock
{
    Task WaitAsync(CancellationToken cancellationToken = default);

    void Release();
}
