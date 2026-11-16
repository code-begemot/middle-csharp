using Microsoft.Extensions.Options;

namespace MiddleCsharp.BackgroundServices;

public sealed class BookingProcessingService : BackgroundService
{
    private readonly BookingProcessor _processor;
    private readonly BookingProcessingOptions _options;
    private readonly ILogger<BookingProcessingService> _logger;

    public BookingProcessingService(
        BookingProcessor processor,
        IOptions<BookingProcessingOptions> options,
        ILogger<BookingProcessingService> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Booking processing started: poll {PollInterval}, processing delay {ProcessingDelay}",
            _options.PollInterval,
            _options.ProcessingDelay);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _processor.ProcessPendingAsync(stoppingToken);
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
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Booking processing stopped");
    }
}
