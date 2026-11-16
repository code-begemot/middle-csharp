using Microsoft.Extensions.Options;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;

namespace MiddleCsharp.BackgroundServices;

public sealed class BookingProcessor
{
    private readonly IBookingStore _bookingStore;
    private readonly ILogger<BookingProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly BookingProcessingOptions _options;

    public BookingProcessor(
        IBookingStore bookingStore,
        IOptions<BookingProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<BookingProcessor> logger)
    {
        _bookingStore = bookingStore;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _bookingStore.GetPending();
        var confirmed = 0;

        foreach (var booking in pending)
        {
            await Task.Delay(_options.ProcessingDelay, cancellationToken);

            try
            {
                var processedAt = _timeProvider.GetUtcNow().UtcDateTime;

                if (_bookingStore.TryUpdateStatus(
                    booking.Id,
                    BookingStatus.Pending,
                    BookingStatus.Confirmed,
                    processedAt))
                {
                    confirmed++;
                    _logger.LogInformation(
                        "Booking {BookingId} for event {EventId} confirmed at {ProcessedAt}",
                        booking.Id,
                        booking.EventId,
                        processedAt);
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

        return confirmed;
    }
}
