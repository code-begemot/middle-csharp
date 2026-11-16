using System.Collections.Concurrent;
using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess;

public sealed class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<Guid, Booking> _bookings = new();

    public void Add(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (!_bookings.TryAdd(booking.Id, booking.Clone()))
        {
            throw new InvalidOperationException($"Booking with id '{booking.Id}' already exists.");
        }
    }

    public Booking? GetById(Guid id)
    {
        return _bookings.TryGetValue(id, out var booking) ? booking.Clone() : null;
    }

    public IReadOnlyList<Booking> GetPending()
    {
        return _bookings.Values
            .Where(booking => booking.Status == BookingStatus.Pending)
            .Select(booking => booking.Clone())
            .ToList();
    }

    public bool TryUpdateStatus(
        Guid id,
        BookingStatus expectedStatus,
        BookingStatus newStatus,
        DateTime processedAt)
    {
        if (newStatus is not (BookingStatus.Confirmed or BookingStatus.Rejected))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                newStatus,
                "New status must be Confirmed or Rejected.");
        }

        while (true)
        {
            if (!_bookings.TryGetValue(id, out var existing) || existing.Status != expectedStatus)
            {
                return false;
            }

            var replacement = existing.Clone();

            if (newStatus == BookingStatus.Confirmed)
            {
                replacement.Confirm(processedAt);
            }
            else
            {
                replacement.Reject(processedAt);
            }

            if (_bookings.TryUpdate(id, replacement, existing))
            {
                return true;
            }
        }
    }
}
