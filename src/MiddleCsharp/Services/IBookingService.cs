using MiddleCsharp.DTOs;

namespace MiddleCsharp.Services;

public interface IBookingService
{
    Task<BookingInfo> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<BookingInfo> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
