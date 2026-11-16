using Microsoft.Extensions.Options;
using MiddleCsharp.DataAccess;
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
        var processed = 0;

        foreach (var booking in pending)
        {
            await Task.Delay(_options.ProcessingDelay, cancellationToken);

            try
            {
                var processedAt = _timeProvider.GetUtcNow().UtcDateTime;
                var targetStatus = _eventService.Exists(booking.EventId)
                    ? BookingStatus.Confirmed
                    : BookingStatus.Rejected;

                if (_bookingStore.TryUpdateStatus(
                    booking.Id,
                    BookingStatus.Pending,
                    targetStatus,
                    processedAt))
                {
                    processed++;

                    if (targetStatus == BookingStatus.Confirmed)
                    {
                        _logger.LogInformation(
                            "Booking {BookingId} for event {EventId} confirmed at {ProcessedAt}",
                            booking.Id,
                            booking.EventId,
                            processedAt);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Booking {BookingId} rejected at {ProcessedAt}: event {EventId} no longer exists",
                            booking.Id,
                            processedAt,
                            booking.EventId);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Booking {BookingId} was skipped because it is no longer pending",
                        booking.Id);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to process booking {BookingId}", booking.Id);
            }
        }

        return processed;
    }
}
