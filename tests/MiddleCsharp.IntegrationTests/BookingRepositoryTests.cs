using Microsoft.EntityFrameworkCore;
using MiddleCsharp.Models;

namespace MiddleCsharp.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class BookingRepositoryTests : RepositoryTestBase
{
    private static readonly DateTime CreatedAt =
        new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);

    public BookingRepositoryTests(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsPendingBooking()
    {
        // Arrange
        var createdEvent = await AddEventAsync();

        // Act
        var booking = await AddPendingBookingAsync(createdEvent.Id);

        // Assert
        var stored = await Bookings.GetByIdAsync(booking.Id);
        Assert.NotNull(stored);
        Assert.Equal(createdEvent.Id, stored.EventId);
        Assert.Equal(BookingStatus.Pending, stored.Status);
        Assert.Equal(CreatedAt, stored.CreatedAt);
        Assert.Null(stored.ProcessedAt);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        // Act
        var stored = await Bookings.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(stored);
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotTrackEntity()
    {
        // Arrange
        var createdEvent = await AddEventAsync();
        var booking = await AddPendingBookingAsync(createdEvent.Id);
        var fetched = await Bookings.GetByIdAsync(booking.Id);
        Assert.NotNull(fetched);

        // Act
        fetched.Confirm(new DateTime(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc));
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var stored = await context.Bookings
            .AsNoTracking()
            .SingleAsync(found => found.Id == booking.Id);
        Assert.Equal(BookingStatus.Pending, stored.Status);
        Assert.Null(stored.ProcessedAt);
    }

    [Fact]
    public async Task GetForProcessingAsync_IncludesEvent()
    {
        // Arrange
        var createdEvent = await AddEventAsync();
        var booking = await AddPendingBookingAsync(createdEvent.Id);

        // Act
        var stored = await Bookings.GetForProcessingAsync(booking.Id);

        // Assert
        Assert.NotNull(stored);
        Assert.NotNull(stored.Event);
        Assert.Equal(createdEvent.Id, stored.Event.Id);
    }

    [Fact]
    public async Task GetPendingIdsAsync_ReturnsOnlyPendingInOrder()
    {
        // Arrange
        var createdEvent = await AddEventAsync();
        var first = await AddPendingBookingAsync(
            createdEvent.Id,
            new DateTime(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc));
        var second = await AddPendingBookingAsync(
            createdEvent.Id,
            new DateTime(2025, 1, 10, 10, 2, 0, DateTimeKind.Utc));
        var confirmed = await AddPendingBookingAsync(
            createdEvent.Id,
            new DateTime(2025, 1, 10, 10, 1, 0, DateTimeKind.Utc));

        var tracked = await Bookings.GetForProcessingAsync(confirmed.Id);
        Assert.NotNull(tracked);
        tracked.Confirm(new DateTime(2025, 1, 10, 10, 5, 0, DateTimeKind.Utc));
        await UnitOfWork.SaveChangesAsync();

        // Act
        var pendingIds = await Bookings.GetPendingIdsAsync();

        // Assert
        Assert.Equal(new[] { first.Id, second.Id }, pendingIds);
    }

    [Fact]
    public async Task GetPendingIdsAsync_NoPendingBookings_ReturnsEmpty()
    {
        // Act
        var pendingIds = await Bookings.GetPendingIdsAsync();

        // Assert
        Assert.Empty(pendingIds);
    }

    [Fact]
    public async Task AddAsync_ReservedSeatsAndBooking_AreSavedTogether()
    {
        // Arrange
        var createdEvent = await AddEventAsync(totalSeats: 1);
        var storedEvent = await Events.GetByIdForUpdateAsync(createdEvent.Id);
        Assert.NotNull(storedEvent);
        Assert.True(storedEvent.TryReserveSeats());

        // Act
        var booking = Booking.CreatePending(storedEvent.Id, CreatedAt);
        await Bookings.AddAsync(booking);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var eventAfterBooking = await context.Events
            .AsNoTracking()
            .SingleAsync(evt => evt.Id == createdEvent.Id);
        Assert.Equal(0, eventAfterBooking.AvailableSeats);
        Assert.True(await context.Bookings.AnyAsync(found => found.Id == booking.Id));
    }

    [Fact]
    public async Task AddAsync_UnknownEvent_ThrowsForeignKeyViolation()
    {
        // Arrange
        var booking = Booking.CreatePending(Guid.NewGuid(), CreatedAt);

        // Act
        await Bookings.AddAsync(booking);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => UnitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task ConfirmAndSave_PersistsStatusAndProcessedAt()
    {
        // Arrange
        var createdEvent = await AddEventAsync();
        var booking = await AddPendingBookingAsync(createdEvent.Id);
        var processedAt = new DateTime(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

        // Act
        var stored = await Bookings.GetForProcessingAsync(booking.Id);
        Assert.NotNull(stored);
        stored.Confirm(processedAt);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var confirmed = await context.Bookings
            .AsNoTracking()
            .SingleAsync(found => found.Id == booking.Id);
        Assert.Equal(BookingStatus.Confirmed, confirmed.Status);
        Assert.Equal(processedAt, confirmed.ProcessedAt);
    }

    [Fact]
    public async Task RejectAndReleaseSeat_PersistsStatusAndSeat()
    {
        // Arrange
        var createdEvent = await AddEventAsync(totalSeats: 2);
        var storedEvent = await Events.GetByIdForUpdateAsync(createdEvent.Id);
        Assert.NotNull(storedEvent);
        Assert.True(storedEvent.TryReserveSeats());

        var booking = Booking.CreatePending(storedEvent.Id, CreatedAt);
        await Bookings.AddAsync(booking);
        await UnitOfWork.SaveChangesAsync();

        var processedAt = new DateTime(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

        // Act
        var storedBooking = await Bookings.GetForProcessingAsync(booking.Id);
        Assert.NotNull(storedBooking);
        storedBooking.Reject(processedAt);
        storedBooking.Event.ReleaseSeats();
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var rejected = await context.Bookings
            .AsNoTracking()
            .SingleAsync(found => found.Id == booking.Id);
        var eventAfterRejection = await context.Events
            .AsNoTracking()
            .SingleAsync(evt => evt.Id == createdEvent.Id);

        Assert.Equal(BookingStatus.Rejected, rejected.Status);
        Assert.Equal(processedAt, rejected.ProcessedAt);
        Assert.Equal(2, eventAfterRejection.AvailableSeats);
    }
}
