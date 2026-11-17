using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests;

internal static class TestData
{
    public static readonly DateTime DefaultStartAt = new(2025, 1, 10, 10, 0, 0);
    public static readonly DateTime DefaultEndAt = new(2025, 1, 10, 11, 0, 0);

    public static EventFilter NoFilter => new(null, null, null);

    public static Event NewEvent(
        string title = "Meeting",
        DateTime? startAt = null,
        DateTime? endAt = null,
        Guid? id = null,
        int totalSeats = 10) => new()
    {
        Id = id ?? Guid.Empty,
        Title = title,
        Description = "Sprint planning",
        StartAt = startAt ?? DefaultStartAt,
        EndAt = endAt ?? DefaultEndAt,
        TotalSeats = totalSeats,
        AvailableSeats = totalSeats
    };
}
