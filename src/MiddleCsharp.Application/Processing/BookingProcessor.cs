using Microsoft.Extensions.Logging;
using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.Concurrency;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Enums;

namespace MiddleCsharp.Application.Processing;

internal sealed class BookingProcessor
{
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISeatReservationLock _seatReservationLock;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingProcessor> _logger;

    public BookingProcessor(
        IBookingRepository bookings,
        IUnitOfWork unitOfWork,
        ISeatReservationLock seatReservationLock,
        TimeProvider timeProvider,
        ILogger<BookingProcessor> logger)
    {
        _bookings = bookings;
        _unitOfWork = unitOfWork;
        _seatReservationLock = seatReservationLock;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var booking = await _bookings.GetByIdForUpdateAsync(bookingId, cancellationToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogWarning(
                    "Booking {BookingId} was skipped because it is missing or no longer pending",
                    bookingId);

                return false;
            }

            var processedAt = _timeProvider.GetUtcNow().UtcDateTime;
            booking.Confirm(processedAt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            await _seatReservationLock.WaitAsync(cancellationToken);

            try
            {
                _unitOfWork.ClearChanges();

                var booking = await _bookings.GetForProcessingAsync(bookingId, cancellationToken);

                if (booking is null || booking.Status != BookingStatus.Pending)
                {
                    return false;
                }

                var processedAt = _timeProvider.GetUtcNow().UtcDateTime;
                booking.Reject(processedAt);
                booking.Event?.ReleaseSeats();
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Booking {BookingId} rejected at {ProcessedAt} after processing error",
                    booking.Id,
                    processedAt);

                return true;
            }
            finally
            {
                _seatReservationLock.Release();
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
