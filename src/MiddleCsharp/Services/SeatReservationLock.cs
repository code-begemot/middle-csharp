namespace MiddleCsharp.Services;

internal static class SeatReservationLock
{
    public static SemaphoreSlim Instance { get; } = new(1, 1);
}
