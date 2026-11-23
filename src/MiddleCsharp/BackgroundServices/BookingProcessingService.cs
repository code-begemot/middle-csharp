using Microsoft.Extensions.Options;
using MiddleCsharp.DataAccess.Repositories;

namespace MiddleCsharp.BackgroundServices;

public sealed class BookingProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BookingProcessingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BookingProcessingService> _logger;

    public BookingProcessingService(
        IServiceScopeFactory scopeFactory,
        IOptions<BookingProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<BookingProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Booking processing started: poll {PollingInterval}, processing delay {ProcessingDelay}",
            _options.PollingInterval,
            _options.ProcessingDelay);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error while processing bookings");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Booking processing stopped");
    }

    internal async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        List<Guid> pendingIds;

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            pendingIds = (await bookings.GetPendingIdsAsync(cancellationToken)).ToList();
        }

        if (pendingIds.Count == 0)
        {
            return 0;
        }

        var bookingIds = string.Join(", ", pendingIds);

        _logger.LogInformation(
            "Processing {Count} pending {BookingLabel} in parallel: {BookingIds}",
            pendingIds.Count,
            pendingIds.Count == 1 ? "booking" : "bookings",
            bookingIds);

        var results = await Task.WhenAll(
            pendingIds.Select(bookingId => ProcessBookingAsync(bookingId, cancellationToken)));

        return results.Count(processed => processed);
    }

    private async Task<bool> ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        await Task.Delay(_options.ProcessingDelay, _timeProvider, cancellationToken);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<BookingProcessor>();

        return await processor.ProcessAsync(bookingId, cancellationToken);
    }
}
