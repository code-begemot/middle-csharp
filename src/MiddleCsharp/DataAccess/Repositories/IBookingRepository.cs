using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess.Repositories;

internal interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> GetForProcessingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
}
