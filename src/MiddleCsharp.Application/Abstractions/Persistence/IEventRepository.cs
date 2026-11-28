using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;

namespace MiddleCsharp.Application.Abstractions.Persistence;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Event?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginatedResult<Event>> GetPageAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Event newEvent, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
