using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Enums;
using MiddleCsharp.Domain.Exceptions;
using MiddleCsharp.Infrastructure.Persistence;

namespace MiddleCsharp.Tests.Services;

public class BookingServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTime ProcessedAt =
        new(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

    private readonly TestApplication _app = new(new FixedTimeProvider(Now));

    private IEventService EventService => _app.Get<IEventService>();
    private IBookingService BookingService => _app.Get<IBookingService>();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_ReturnsPendingBooking()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());

        // Act
        var booking = await BookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(createdEvent.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(Now.UtcDateTime, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);
        Assert.Equal(1, await CountPendingAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_HaveUniqueIds()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());

        // Act
        var first = await BookingService.CreateBookingAsync(createdEvent.Id);
        var second = await BookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await CountPendingAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_UnknownEvent_ThrowsNotFound()
    {
        // Arrange
        var unknownEventId = Guid.NewGuid();

        // Act
        var exception = await Assert.ThrowsAsync<EventNotFoundException>(
            () => BookingService.CreateBookingAsync(unknownEventId));

        // Assert
        Assert.Equal(unknownEventId, exception.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_DeletedEvent_ThrowsNotFound()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());
        await EventService.DeleteAsync(createdEvent.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => BookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsBookingInfo()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());
        var created = await BookingService.CreateBookingAsync(createdEvent.Id);

        // Act
        var booking = await BookingService.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(created.Id, booking.Id);
        Assert.Equal(createdEvent.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(created.CreatedAt, booking.CreatedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReflectsConfirmedStatus()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());
        var created = await BookingService.CreateBookingAsync(createdEvent.Id);

        var context = _app.Get<AppDbContext>();
        var stored = await context.Bookings.SingleAsync(booking => booking.Id == created.Id);
        stored.Confirm(ProcessedAt);
        await context.SaveChangesAsync();

        // Act
        var booking = await BookingService.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReflectsRejectedStatus()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent());
        var created = await BookingService.CreateBookingAsync(createdEvent.Id);

        var context = _app.Get<AppDbContext>();
        var stored = await context.Bookings.SingleAsync(booking => booking.Id == created.Id);
        stored.Reject(ProcessedAt);
        await context.SaveChangesAsync();

        // Act
        var booking = await BookingService.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var exception = await Assert.ThrowsAsync<BookingNotFoundException>(
            () => BookingService.GetBookingByIdAsync(unknownId));

        // Assert
        Assert.Equal(unknownId, exception.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_DecrementsAvailableSeats()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 3));

        // Act
        await BookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        var stored = await EventService.GetByIdAsync(createdEvent.Id);
        Assert.Equal(3, stored.TotalSeats);
        Assert.Equal(2, stored.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_UpToSeatLimit_SucceedsAndThenReturnsConflict()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 3));

        // Act
        var bookings = new List<BookingInfo>();
        var availableSeatsAfterEachBooking = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            bookings.Add(await BookingService.CreateBookingAsync(createdEvent.Id));
            availableSeatsAfterEachBooking.Add(
                (await EventService.GetByIdAsync(createdEvent.Id)).AvailableSeats);
        }

        // Assert
        Assert.Equal(new[] { 2, 1, 0 }, availableSeatsAfterEachBooking);
        Assert.Equal(3, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.Equal(0, (await EventService.GetByIdAsync(createdEvent.Id)).AvailableSeats);
        Assert.Equal(3, await CountPendingAsync());

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => BookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_NoSeatsLeft_ThrowsNoAvailableSeats()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 1));
        await BookingService.CreateBookingAsync(createdEvent.Id);

        // Act
        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => BookingService.CreateBookingAsync(createdEvent.Id));

        // Assert
        Assert.Equal("No available seats for this event", exception.Message);
        Assert.Equal(1, await CountPendingAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequests_DoesNotOverbook()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 5));

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => TryBookAsync(createdEvent.Id))));

        // Assert
        Assert.Equal(5, results.Count(succeeded => succeeded));
        Assert.Equal(15, results.Count(succeeded => !succeeded));
        Assert.Equal(0, (await EventService.GetByIdAsync(createdEvent.Id)).AvailableSeats);
        Assert.Equal(5, await CountPendingAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequestsWithEnoughSeats_HaveUniqueIds()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 10));

        // Act
        var bookings = await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => CreateBookingInNewScopeAsync(createdEvent.Id))));

        // Assert
        Assert.Equal(10, bookings.Select(booking => booking.Id).Distinct().Count());
        Assert.Equal(0, (await EventService.GetByIdAsync(createdEvent.Id)).AvailableSeats);
    }

    [Fact]
    public async Task GetBookingByIdAsync_DoesNotConsumeSeats()
    {
        // Arrange
        var createdEvent = await EventService.CreateAsync(TestData.NewEvent(totalSeats: 3));
        var created = await BookingService.CreateBookingAsync(createdEvent.Id);

        // Act
        await BookingService.GetBookingByIdAsync(created.Id);
        await BookingService.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(2, (await EventService.GetByIdAsync(createdEvent.Id)).AvailableSeats);
    }

    private async Task<int> CountPendingAsync()
    {
        using var scope = _app.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Bookings.CountAsync(booking => booking.Status == BookingStatus.Pending);
    }

    private async Task<bool> TryBookAsync(Guid eventId)
    {
        try
        {
            await CreateBookingInNewScopeAsync(eventId);
            return true;
        }
        catch (NoAvailableSeatsException)
        {
            return false;
        }
    }

    private async Task<BookingInfo> CreateBookingInNewScopeAsync(Guid eventId)
    {
        using var scope = _app.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        return await service.CreateBookingAsync(eventId);
    }
}
