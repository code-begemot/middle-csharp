using MiddleCsharp.Application.Commands;
using MiddleCsharp.Application.DTOs;

namespace MiddleCsharp.Tests;

internal static class TestData
{
    public static readonly DateTime DefaultStartAt = new(2025, 1, 10, 10, 0, 0);
    public static readonly DateTime DefaultEndAt = new(2025, 1, 10, 11, 0, 0);

    public static EventFilter NoFilter => new(null, null, null);

    public static CreateEventCommand NewCreateCommand(
        string title = "Meeting",
        DateTime? startAt = null,
        DateTime? endAt = null,
        int totalSeats = 10) => new(
            title,
            "Sprint planning",
            startAt ?? DefaultStartAt,
            endAt ?? DefaultEndAt,
            totalSeats);

    public static UpdateEventCommand NewUpdateCommand(
        string title = "Meeting",
        DateTime? startAt = null,
        DateTime? endAt = null) => new(
            title,
            "Sprint planning",
            startAt ?? DefaultStartAt,
            endAt ?? DefaultEndAt);
}
