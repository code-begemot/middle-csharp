using MiddleCsharp.DataAccess;
using MiddleCsharp.DTOs;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public sealed class BookingService : IBookingService
{
    private readonly object _bookingLock = new();
    private readonly IEventService _eventService;
    private readonly IBookingStore _bookingStore;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IEventService eventService,
        IBookingStore bookingStore,
        TimeProvider timeProvider)
    {
        _eventService = eventService;
        _bookingStore = bookingStore;
        _timeProvider = timeProvider;
    }

    public Task<BookingInfo> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            if (!_eventService.TryReserveSeats(eventId))
            {
                throw new NoAvailableSeatsException();
            }

            var booking = Booking.CreatePending(eventId, _timeProvider.GetUtcNow().UtcDateTime);

            try
            {
                _bookingStore.Add(booking);
            }
            catch
            {
                _eventService.ReleaseSeats(eventId);
                throw;
            }

            return Task.FromResult(booking.ToInfo());
        }
    }

    public Task<BookingInfo> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId)
            ?? throw new BookingNotFoundException(bookingId);

        return Task.FromResult(booking.ToInfo());
    }
}
