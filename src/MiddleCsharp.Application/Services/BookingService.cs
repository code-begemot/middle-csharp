using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.Concurrency;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Exceptions;

namespace MiddleCsharp.Application.Services;

internal sealed class BookingService : IBookingService
{
    private readonly IEventRepository _events;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeatReservationLock _seatReservationLock;
    private readonly TimeProvider _timeProvider;

    public BookingService(
        IEventRepository events,
        IBookingRepository bookings,
        IUnitOfWork unitOfWork,
        ISeatReservationLock seatReservationLock,
        TimeProvider timeProvider)
    {
        _events = events;
        _bookings = bookings;
        _unitOfWork = unitOfWork;
        _seatReservationLock = seatReservationLock;
        _timeProvider = timeProvider;
    }

    public async Task<BookingInfo> CreateBookingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await _seatReservationLock.WaitAsync(cancellationToken);

        Booking booking;

        try
        {
            var evt = await _events.GetByIdForUpdateAsync(eventId, cancellationToken)
                ?? throw new EventNotFoundException(eventId);

            if (!evt.TryReserveSeats())
            {
                throw new NoAvailableSeatsException();
            }

            booking = Booking.CreatePending(eventId, _timeProvider.GetUtcNow().UtcDateTime);

            await _bookings.AddAsync(booking, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _unitOfWork.ClearChanges();
            throw;
        }
        finally
        {
            _seatReservationLock.Release();
        }

        return booking.ToInfo();
    }

    public async Task<BookingInfo> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, cancellationToken);

        return booking?.ToInfo() ?? throw new BookingNotFoundException(bookingId);
    }
}
