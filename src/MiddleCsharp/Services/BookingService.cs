using Microsoft.EntityFrameworkCore;
using MiddleCsharp.DataAccess;
using MiddleCsharp.DTOs;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

internal sealed class BookingService : IBookingService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public BookingService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<BookingInfo> CreateBookingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await SeatReservationLock.Instance.WaitAsync(cancellationToken);

        Booking booking;

        try
        {
            var evt = await _context.Events
                .SingleOrDefaultAsync(stored => stored.Id == eventId, cancellationToken)
                ?? throw new EventNotFoundException(eventId);

            if (!evt.TryReserveSeats())
            {
                throw new NoAvailableSeatsException();
            }

            booking = Booking.CreatePending(eventId, _timeProvider.GetUtcNow().UtcDateTime);

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _context.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            SeatReservationLock.Instance.Release();
        }

        return booking.ToInfo();
    }

    public async Task<BookingInfo> GetBookingByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(stored => stored.Id == bookingId, cancellationToken);

        return booking?.ToInfo() ?? throw new BookingNotFoundException(bookingId);
    }
}
