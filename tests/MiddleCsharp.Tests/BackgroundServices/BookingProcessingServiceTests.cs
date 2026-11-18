using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiddleCsharp.BackgroundServices;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.BackgroundServices;

public class BookingProcessingServiceTests
{
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 5, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_PendingBooking_ProcessesItAndStopsByCancellation()
    {
        // Arrange
        var eventService = new EventService();
        var eventId = eventService.Create(TestData.NewEvent()).Id;
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(eventId, CreatedAt);
        store.Add(booking);
        var service = CreateService(store, eventService);

        // Act
        await service.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.GetById(booking.Id)!.Status == BookingStatus.Pending &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, store.GetById(booking.Id)!.Status);
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_NoPendingBookings_StopsWithoutErrors()
    {
        // Arrange
        var service = CreateService(new InMemoryBookingStore(), new EventService());

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    private static BookingProcessingService CreateService(
        IBookingStore store,
        IEventService eventService)
    {
        var options = Options.Create(new BookingProcessingOptions
        {
            PollingInterval = TimeSpan.FromMilliseconds(20),
            ProcessingDelay = TimeSpan.Zero
        });

        var processor = new BookingProcessor(
            store,
            eventService,
            options,
            new FixedTimeProvider(Now),
            NullLogger<BookingProcessor>.Instance);

        return new BookingProcessingService(
            processor,
            options,
            NullLogger<BookingProcessingService>.Instance);
    }
}
