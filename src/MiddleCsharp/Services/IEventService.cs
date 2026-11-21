using MiddleCsharp.DTOs;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public interface IEventService
{
    Task<Event> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<Event>> GetAllAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<Event> CreateAsync(Event newEvent, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, Event updatedEvent, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
