using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Application.Processing;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Enums;
using MiddleCsharp.Infrastructure.Persistence;

namespace MiddleCsharp.Tests.BackgroundServices;

public class BookingProcessingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 5, TimeSpan.Zero);

    [Fact]
    public async Task ProcessPendingAsync_PendingBooking_ConfirmsIt()
    {
        // Arrange
        using var app = new TestApplication(new FixedTimeProvider(Now));
        var eventId = await CreateEventAsync(app);
        var booking = await CreateBookingAsync(app, eventId);
        var service = CreateService(app, new FixedTimeProvider(Now), TimeSpan.Zero, TimeSpan.Zero);

        // Act
        var processed = await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(app, booking.Id));
    }

    [Fact]
    public async Task ProcessPendingAsync_NoPendingBookings_ReturnsZero()
    {
        // Arrange
        using var app = new TestApplication(new FixedTimeProvider(Now));
        var service = CreateService(app, new FixedTimeProvider(Now), TimeSpan.Zero, TimeSpan.Zero);

        // Act
        var processed = await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task ProcessPendingAsync_MultiplePendingBookings_ConfirmsAll()
    {
        // Arrange
        using var app = new TestApplication(new FixedTimeProvider(Now));
        var eventId = await CreateEventAsync(app);
        var first = await CreateBookingAsync(app, eventId);
        var second = await CreateBookingAsync(app, eventId);
        var service = CreateService(app, new FixedTimeProvider(Now), TimeSpan.Zero, TimeSpan.Zero);

        // Act
        var processed = await service.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, processed);
        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(app, first.Id));
        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(app, second.Id));
    }

    [Fact]
    public async Task ProcessPendingAsync_MultiplePendingBookings_StartsDelaysInParallel()
    {
        // Arrange
        using var app = new TestApplication(new FixedTimeProvider(Now));
        var eventId = await CreateEventAsync(app);
        var bookings = new List<BookingInfo>();

        for (var i = 0; i < 4; i++)
        {
            bookings.Add(await CreateBookingAsync(app, eventId));
        }

        var timeProvider = new DelayTrackingTimeProvider(bookings.Count);
        var service = CreateService(
            app,
            timeProvider,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30));
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var processing = service.ProcessPendingAsync(cancellationTokenSource.Token);

        try
        {
            await timeProvider.ExpectedDelaysStarted.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            cancellationTokenSource.Cancel();
        }

        // Assert
        Assert.Equal(bookings.Count, timeProvider.MaxActiveDelays);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);

        foreach (var booking in bookings)
        {
            Assert.Equal(BookingStatus.Pending, await GetStatusAsync(app, booking.Id));
        }
    }

    [Fact]
    public async Task ExecuteAsync_PendingBooking_ProcessesItAndStopsByCancellation()
    {
        // Arrange
        using var app = new TestApplication();
        var eventId = await CreateEventAsync(app);
        var booking = await CreateBookingAsync(app, eventId);
        var service = CreateService(
            app,
            TimeProvider.System,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.Zero);

        // Act
        await service.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (await GetStatusAsync(app, booking.Id) == BookingStatus.Pending &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(app, booking.Id));
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_NoPendingBookings_StopsWithoutErrors()
    {
        // Arrange
        using var app = new TestApplication();
        var service = CreateService(
            app,
            TimeProvider.System,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.Zero);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    private static BookingProcessingService CreateService(
        TestApplication app,
        TimeProvider timeProvider,
        TimeSpan pollingInterval,
        TimeSpan processingDelay)
    {
        var options = Options.Create(new BookingProcessingOptions
        {
            PollingInterval = pollingInterval,
            ProcessingDelay = processingDelay
        });

        return new BookingProcessingService(
            app.ScopeFactory,
            options,
            timeProvider,
            NullLogger<BookingProcessingService>.Instance);
    }

    private static async Task<Guid> CreateEventAsync(TestApplication app)
    {
        using var scope = app.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();

        return (await eventService.CreateAsync(TestData.NewEvent(totalSeats: 10))).Id;
    }

    private static async Task<BookingInfo> CreateBookingAsync(TestApplication app, Guid eventId)
    {
        using var scope = app.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        return await bookingService.CreateBookingAsync(eventId);
    }

    private static async Task<BookingStatus> GetStatusAsync(TestApplication app, Guid bookingId)
    {
        using var scope = app.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return (await context.Bookings
            .AsNoTracking()
            .SingleAsync(booking => booking.Id == bookingId))
            .Status;
    }
}
