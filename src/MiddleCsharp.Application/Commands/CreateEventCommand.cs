namespace MiddleCsharp.Application.Commands;

public sealed record CreateEventCommand(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    int TotalSeats);
