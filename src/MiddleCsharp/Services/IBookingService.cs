using MiddleCsharp.DTOs;

namespace MiddleCsharp.Services;

public interface IBookingService
{
    Task<BookingInfo> CreateBookingAsync(Guid eventId);
    Task<BookingInfo> GetBookingByIdAsync(Guid bookingId);
}
