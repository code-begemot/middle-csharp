using Microsoft.EntityFrameworkCore;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.BackgroundServices;

internal sealed class BookingProcessor
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingProcessor> _logger;

    public BookingProcessor(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<BookingProcessor> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var booking = await _context.Bookings
                .SingleOrDefaultAsync(stored => stored.Id == bookingId, cancellationToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogWarning(
                    "Booking {BookingId} was skipped because it is missing or no longer pending",
                    bookingId);

                return false;
            }

            var processedAt = _timeProvider.GetUtcNow().UtcDateTime;
            booking.Confirm(processedAt);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Booking {BookingId} for event {EventId} confirmed at {ProcessedAt}",
                booking.Id,
                booking.EventId,
                processedAt);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process booking {BookingId}", bookingId);
            return await RejectAfterFailureAsync(bookingId, cancellationToken);
        }
    }

    private async Task<bool> RejectAfterFailureAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            await SeatReservationLock.Instance.WaitAsync(cancellationToken);

            try
            {
                _context.ChangeTracker.Clear();

                var booking = await _context.Bookings
                    .Include(stored => stored.Event)
                    .SingleOrDefaultAsync(stored => stored.Id == bookingId, cancellationToken);

                if (booking is null || booking.Status != BookingStatus.Pending)
                {
                    return false;
                }

                var processedAt = _timeProvider.GetUtcNow().UtcDateTime;
                booking.Reject(processedAt);
                booking.Event?.ReleaseSeats();
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Booking {BookingId} rejected at {ProcessedAt} after processing error",
                    booking.Id,
                    processedAt);

                return true;
            }
            finally
            {
                SeatReservationLock.Instance.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception rejectException)
        {
            _logger.LogError(
                rejectException,
                "Failed to reject booking {BookingId} after processing error",
                bookingId);

            return false;
        }
    }
}
