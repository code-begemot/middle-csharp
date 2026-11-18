using MiddleCsharp.Models;

namespace MiddleCsharp.DTOs;

public static class EventMappingExtensions
{
    public static Event ToEntity(this CreateEventRequest request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        StartAt = request.StartAt ?? default,
        EndAt = request.EndAt ?? default,
        TotalSeats = request.TotalSeats ?? default
    };

    public static Event ToEntity(this UpdateEventRequest request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        StartAt = request.StartAt ?? default,
        EndAt = request.EndAt ?? default
    };

    public static EventResponse ToResponse(this Event evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartAt = evt.StartAt,
        EndAt = evt.EndAt,
        TotalSeats = evt.TotalSeats,
        AvailableSeats = evt.AvailableSeats
    };

    public static PaginatedResult<EventResponse> ToResponse(this PaginatedResult<Event> result) => new()
    {
        TotalCount = result.TotalCount,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalPages = result.TotalPages,
        Items = result.Items.Select(ToResponse).ToList()
    };
}
