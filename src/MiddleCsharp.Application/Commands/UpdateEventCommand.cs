namespace MiddleCsharp.Application.Commands;

public sealed record UpdateEventCommand(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt);
