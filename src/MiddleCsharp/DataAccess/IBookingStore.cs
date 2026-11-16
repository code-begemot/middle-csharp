using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess;

public interface IBookingStore
{
    void Add(Booking booking);
    Booking? GetById(Guid id);
    IReadOnlyList<Booking> GetPending();
    bool TryUpdateStatus(Guid id, BookingStatus expectedStatus, BookingStatus newStatus, DateTime processedAt);
}
