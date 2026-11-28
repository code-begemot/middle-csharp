using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiddleCsharp.Application.Concurrency;
using MiddleCsharp.Application.Processing;
using MiddleCsharp.Application.Services;

namespace MiddleCsharp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISeatReservationLock, SeatReservationLock>();

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<BookingProcessor>();

        services
            .AddOptions<BookingProcessingOptions>()
            .Bind(configuration.GetSection(BookingProcessingOptions.SectionName))
            .Validate(
                options => options.PollingInterval > TimeSpan.Zero,
                "BookingProcessing:PollingInterval must be greater than zero.")
            .Validate(
                options => options.ProcessingDelay >= TimeSpan.Zero,
                "BookingProcessing:ProcessingDelay must be zero or greater.")
            .ValidateOnStart();

        services.AddHostedService<BookingProcessingService>();

        return services;
    }
}
