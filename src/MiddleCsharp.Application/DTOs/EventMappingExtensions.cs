using MiddleCsharp.Application.Commands;
using MiddleCsharp.Domain.Entities;

namespace MiddleCsharp.Application.DTOs;

public static class EventMappingExtensions
{
    public static CreateEventCommand ToCommand(this CreateEventRequest request) => new(
        request.Title,
        request.Description,
        request.StartAt ?? default,
        request.EndAt ?? default,
        request.TotalSeats ?? default);

    public static UpdateEventCommand ToCommand(this UpdateEventRequest request) => new(
        request.Title,
        request.Description,
        request.StartAt ?? default,
        request.EndAt ?? default);

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
        Items = result.Items.Select(ToResponse).ToList()
    };
}
