using MiddleCsharp.Domain.Entities;

namespace MiddleCsharp.Application.Abstractions.Persistence;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Booking?> GetForProcessingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
}
