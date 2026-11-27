namespace MiddleCsharp.Application.DTOs;

public sealed record EventFilter
{
    public EventFilter(string? title, DateTime? from, DateTime? to)
    {
        Title = title;
        From = from.HasValue ? Normalize(from.Value) : null;
        To = to.HasValue ? Normalize(to.Value) : null;
    }

    public string? Title { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    private static DateTime Normalize(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
