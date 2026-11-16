using MiddleCsharp.Models;

namespace MiddleCsharp.DTOs;

public static class BookingMappingExtensions
{
    public static BookingInfo ToInfo(this Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}
