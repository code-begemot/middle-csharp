using MiddleCsharp.DataAccess;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class BookingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTime ProcessedAt =
        new(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

    private readonly EventService _eventService = new();
    private readonly InMemoryBookingStore _bookingStore = new();
    private readonly FixedTimeProvider _timeProvider = new(Now);

    private BookingService CreateService() =>
        new(_eventService, _bookingStore, _timeProvider);

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_ReturnsPendingBooking()
    {
        // Arrange
        var createdEvent = _eventService.Create(TestData.NewEvent());

        // Act
        var booking = await CreateService().CreateBookingAsync(createdEvent.Id);

        // Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(createdEvent.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(Now.UtcDateTime, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);
        Assert.NotNull(_bookingStore.GetById(booking.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_HaveUniqueIds()
    {
        // Arrange
        var createdEvent = _eventService.Create(TestData.NewEvent());
        var service = CreateService();

        // Act
        var first = await service.CreateBookingAsync(createdEvent.Id);
        var second = await service.CreateBookingAsync(createdEvent.Id);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, _bookingStore.GetPending().Count);
    }

    [Fact]
    public async Task CreateBookingAsync_UnknownEvent_ThrowsNotFound()
    {
        // Arrange
        var unknownEventId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EventNotFoundException>(
            () => CreateService().CreateBookingAsync(unknownEventId));

        Assert.Equal(unknownEventId, exception.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_DeletedEvent_ThrowsNotFound()
    {
        // Arrange
        var createdEvent = _eventService.Create(TestData.NewEvent());
        _eventService.Delete(createdEvent.Id);

        // Act & Assert
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => CreateService().CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsBookingInfo()
    {
        // Arrange
        var createdEvent = _eventService.Create(TestData.NewEvent());
        var service = CreateService();
        var created = await service.CreateBookingAsync(createdEvent.Id);

        // Act
        var booking = await service.GetBookingByIdAsync(created.Id);

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
        var createdEvent = _eventService.Create(TestData.NewEvent());
        var service = CreateService();
        var created = await service.CreateBookingAsync(createdEvent.Id);
        _bookingStore.TryUpdateStatus(
            created.Id,
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt);

        // Act
        var booking = await service.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReflectsRejectedStatus()
    {
        // Arrange
        var createdEvent = _eventService.Create(TestData.NewEvent());
        var service = CreateService();
        var created = await service.CreateBookingAsync(createdEvent.Id);
        _bookingStore.TryUpdateStatus(
            created.Id,
            BookingStatus.Pending,
            BookingStatus.Rejected,
            ProcessedAt);

        // Act
        var booking = await service.GetBookingByIdAsync(created.Id);

        // Assert
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BookingNotFoundException>(
            () => CreateService().GetBookingByIdAsync(unknownId));

        Assert.Equal(unknownId, exception.Id);
    }
}
