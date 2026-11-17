using Microsoft.Extensions.Options;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.BackgroundServices;

public sealed class BookingProcessor
{
    private readonly IBookingStore _bookingStore;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly BookingProcessingOptions _options;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    public BookingProcessor(
        IBookingStore bookingStore,
        IEventService eventService,
        IOptions<BookingProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<BookingProcessor> logger)
    {
        _bookingStore = bookingStore;
        _eventService = eventService;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _bookingStore.GetPending();

        if (pending.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "Processing {Count} pending bookings in parallel",
            pending.Count);

        var tasks = pending.Select(booking => ProcessBookingAsync(booking, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.Count(processed => processed);
    }

    private async Task<bool> ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_options.ProcessingDelay, stoppingToken);

            await _processingSemaphore.WaitAsync(stoppingToken);

            try
            {
                if (!_eventService.Exists(booking.EventId))
                {
                    return RejectMissingEvent(booking);
                }

                var processedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (!_bookingStore.TryUpdateStatus(
                    booking.Id,
                    BookingStatus.Pending,
                    BookingStatus.Confirmed,
                    processedAt))
                {
                    _logger.LogWarning(
                        "Booking {BookingId} was skipped because it is no longer pending",
                        booking.Id);

                    return false;
                }

                _logger.LogInformation(
                    "Booking {BookingId} for event {EventId} confirmed at {ProcessedAt}",
                    booking.Id,
                    booking.EventId,
                    processedAt);

                return true;
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await RejectAfterFailureAsync(booking, exception);
        }
    }

    private bool RejectMissingEvent(Booking booking)
    {
        var processedAt = _timeProvider.GetUtcNow().UtcDateTime;

        if (!_bookingStore.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Rejected,
            processedAt))
        {
            _logger.LogWarning(
                "Booking {BookingId} was skipped because it is no longer pending",
                booking.Id);

            return false;
        }

        _logger.LogWarning(
            "Booking {BookingId} rejected at {ProcessedAt}: event {EventId} no longer exists",
            booking.Id,
            processedAt,
            booking.EventId);

        return true;
    }

    private async Task<bool> RejectAfterFailureAsync(Booking booking, Exception exception)
    {
        _logger.LogError(exception, "Failed to process booking {BookingId}", booking.Id);

        await _processingSemaphore.WaitAsync();

        try
        {
            var processedAt = _timeProvider.GetUtcNow().UtcDateTime;

            if (!_bookingStore.TryUpdateStatus(
                booking.Id,
                BookingStatus.Pending,
                BookingStatus.Rejected,
                processedAt))
            {
                return false;
            }

            ReleaseSeat(booking.EventId);

            _logger.LogWarning(
                "Booking {BookingId} rejected at {ProcessedAt} after processing error",
                booking.Id,
                processedAt);

            return true;
        }
        catch (Exception rejectException)
        {
            _logger.LogError(
                rejectException,
                "Failed to reject booking {BookingId} after processing error",
                booking.Id);

            return false;
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private void ReleaseSeat(Guid eventId)
    {
        try
        {
            _eventService.ReleaseSeats(eventId);
        }
        catch (EventNotFoundException)
        {
            _logger.LogWarning(
                "Cannot release a seat for event {EventId} because it no longer exists",
                eventId);
        }
    }
}
