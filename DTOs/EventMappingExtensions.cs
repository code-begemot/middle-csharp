using MiddleCsharp.Models;

namespace MiddleCsharp.DTOs;

public static class EventMappingExtensions
{
    public static Event ToEntity(this CreateEventRequest request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        StartAt = request.StartAt,
        EndAt = request.EndAt
    };

    public static Event ToEntity(this UpdateEventRequest request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        StartAt = request.StartAt,
        EndAt = request.EndAt
    };

    public static EventResponse ToResponse(this Event evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartAt = evt.StartAt,
        EndAt = evt.EndAt
    };

    public static IEnumerable<EventResponse> ToResponse(this IEnumerable<Event> events)
        => events.Select(ToResponse);
}