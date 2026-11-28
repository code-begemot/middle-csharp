using MiddleCsharp.Application.Commands;
using MiddleCsharp.Application.DTOs;

namespace MiddleCsharp.Application.Services;

public interface IEventService
{
    Task<EventResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginatedResult<EventResponse>> GetAllAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<EventResponse> CreateAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid id,
        UpdateEventCommand command,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
