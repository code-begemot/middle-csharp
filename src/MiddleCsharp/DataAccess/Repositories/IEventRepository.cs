using MiddleCsharp.DTOs;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.DataAccess.Repositories;

internal interface IEventRepository
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
