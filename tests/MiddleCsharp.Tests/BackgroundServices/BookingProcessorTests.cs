using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MiddleCsharp.Application.Processing;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Enums;
using MiddleCsharp.Infrastructure.Persistence;

namespace MiddleCsharp.Tests.BackgroundServices;

public class BookingProcessorTests : IDisposable
{
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 5, TimeSpan.Zero);

    private readonly TestApplication _app;

    public BookingProcessorTests()
    {
        _app = new TestApplication(new FixedTimeProvider(Now));
    }

    public void Dispose() => _app.Dispose();

    private BookingProcessor Processor => _app.Get<BookingProcessor>();
    private IEventService EventService => _app.Get<IEventService>();
    private IBookingService BookingService => _app.Get<IBookingService>();

    [Fact]
    public async Task ProcessAsync_PendingBooking_ConfirmsIt()
    {
        // Arrange
        var eventId = await CreateEventAsync();
        var bookingId = await CreateBookingDirectlyAsync(eventId);

        // Act
        var processed = await Processor.ProcessAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.True(processed);
        var stored = await GetBookingAsync(bookingId);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Equal(Now.UtcDateTime, stored.ProcessedAt);
    }

    [Fact]
    public async Task ProcessAsync_UnknownBooking_ReturnsFalse()
    {
        // Act
        var processed = await Processor.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(processed);
    }

    [Fact]
    public async Task ProcessAsync_AlreadyProcessedBooking_IsSkipped()
    {
        // Arrange
        var eventId = await CreateEventAsync();
        var bookingId = await CreateBookingDirectlyAsync(eventId);

        using (var scope = _app.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings.SingleAsync(stored => stored.Id == bookingId);
            booking.Reject(CreatedAt.AddSeconds(1));
            await context.SaveChangesAsync();
        }

        // Act
        var processed = await Processor.ProcessAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.False(processed);
        Assert.Equal(BookingStatus.Rejected, (await GetBookingAsync(bookingId)).Status);
    }

    [Fact]
    public async Task ProcessAsync_ConfirmedBooking_KeepsReservedSeat()
    {
        // Arrange
        var eventId = await CreateEventAsync(totalSeats: 2);
        var booking = await BookingService.CreateBookingAsync(eventId);

        // Act
        await Processor.ProcessAsync(booking.Id, CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, (await GetBookingAsync(booking.Id)).Status);
        Assert.Equal(1, (await EventService.GetByIdAsync(eventId)).AvailableSeats);
    }

    [Fact]
    public async Task ProcessAsync_ConfirmFailure_RejectsBookingAndReleasesSeat()
    {
        // Arrange
        var interceptor = new FailOnceOnConfirmInterceptor();
        using var app = new TestApplication(
            new FixedTimeProvider(Now),
            options => options.AddInterceptors(interceptor));

        Guid eventId;
        Guid bookingId;

        using (var scope = app.CreateScope())
        {
            var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            eventId = (await eventService.CreateAsync(TestData.NewCreateCommand(totalSeats: 2))).Id;
            bookingId = (await bookingService.CreateBookingAsync(eventId)).Id;
        }

        using var processorScope = app.CreateScope();
        var processor = processorScope.ServiceProvider.GetRequiredService<BookingProcessor>();

        // Act
        var processed = await processor.ProcessAsync(bookingId, CancellationToken.None);

        // Assert
        Assert.True(processed);
        using var assertScope = app.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedBooking = await context.Bookings.SingleAsync(stored => stored.Id == bookingId);
        var storedEvent = await context.Events.SingleAsync(stored => stored.Id == eventId);
        Assert.Equal(BookingStatus.Rejected, storedBooking.Status);
        Assert.Equal(2, storedEvent.AvailableSeats);
    }

    [Fact]
    public async Task ProcessAsync_Cancellation_ThrowsAndLeavesBookingPending()
    {
        // Arrange
        var eventId = await CreateEventAsync();
        var bookingId = await CreateBookingDirectlyAsync(eventId);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Processor.ProcessAsync(bookingId, cancellationTokenSource.Token));

        Assert.Equal(BookingStatus.Pending, (await GetBookingAsync(bookingId)).Status);
    }

    private async Task<Guid> CreateEventAsync(int totalSeats = 10) =>
        (await EventService.CreateAsync(TestData.NewCreateCommand(totalSeats: totalSeats))).Id;

    private async Task<Guid> CreateBookingDirectlyAsync(Guid eventId)
    {
        using var scope = _app.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = Booking.CreatePending(eventId, CreatedAt);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        return booking.Id;
    }

    private async Task<Booking> GetBookingAsync(Guid bookingId)
    {
        using var scope = _app.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Bookings.AsNoTracking().SingleAsync(stored => stored.Id == bookingId);
    }

    private sealed class FailOnceOnConfirmInterceptor : SaveChangesInterceptor
    {
        private int _failed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var hasConfirmedBooking = eventData.Context?.ChangeTracker
                .Entries<Booking>()
                .Any(entry =>
                    entry.State == EntityState.Modified &&
                    entry.Entity.Status == BookingStatus.Confirmed) == true;

            if (hasConfirmedBooking && Interlocked.Exchange(ref _failed, 1) == 0)
            {
                throw new InvalidOperationException("Simulated confirm failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
