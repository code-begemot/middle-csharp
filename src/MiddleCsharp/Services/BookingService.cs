using MiddleCsharp.DataAccess;
using MiddleCsharp.DTOs;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public sealed class BookingService : IBookingService
{
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
        _eventService.GetById(eventId);

        var booking = Booking.CreatePending(eventId, _timeProvider.GetUtcNow().UtcDateTime);
        _bookingStore.Add(booking);

        return Task.FromResult(booking.ToInfo());
    }

    public Task<BookingInfo> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId)
            ?? throw new BookingNotFoundException(bookingId);

        return Task.FromResult(booking.ToInfo());
    }
}
