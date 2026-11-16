using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;

namespace MiddleCsharp.Tests.DataAccess;

public class InMemoryBookingStoreTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ProcessedAt = new(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

    private readonly InMemoryBookingStore _store = new();

    [Fact]
    public void Add_ThenGetById_ReturnsStoredBooking()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);

        // Act
        _store.Add(booking);
        var stored = _store.GetById(booking.Id);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(booking.Id, stored.Id);
        Assert.Equal(EventId, stored.EventId);
        Assert.Equal(BookingStatus.Pending, stored.Status);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        // Act
        var stored = _store.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(stored);
    }

    [Fact]
    public void GetById_ReturnsDetachedCopy()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);

        // Act
        var detached = _store.GetById(booking.Id);
        detached!.Confirm(ProcessedAt);

        // Assert
        Assert.Equal(BookingStatus.Pending, _store.GetById(booking.Id)!.Status);
    }

    [Fact]
    public void Add_DuplicateId_Throws()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _store.Add(booking.Clone()));
    }

    [Fact]
    public void GetPending_ReturnsOnlyPendingBookings()
    {
        // Arrange
        var pending = Booking.CreatePending(EventId, CreatedAt);
        var confirmed = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(pending);
        _store.Add(confirmed);
        _store.TryUpdateStatus(
            confirmed.Id,
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt);

        // Act
        var result = _store.GetPending();

        // Assert
        var item = Assert.Single(result);
        Assert.Equal(pending.Id, item.Id);
    }

    [Fact]
    public void TryUpdateStatus_PendingToConfirmed_SetsProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);

        // Act
        var updated = _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt);

        // Assert
        Assert.True(updated);
        var stored = _store.GetById(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, stored!.Status);
        Assert.Equal(ProcessedAt, stored.ProcessedAt);
    }

    [Fact]
    public void TryUpdateStatus_PendingToRejected_SetsProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);

        // Act
        var updated = _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Rejected,
            ProcessedAt);

        // Assert
        Assert.True(updated);
        var stored = _store.GetById(booking.Id);
        Assert.Equal(BookingStatus.Rejected, stored!.Status);
        Assert.Equal(ProcessedAt, stored.ProcessedAt);
    }

    [Fact]
    public void TryUpdateStatus_AlreadyProcessed_ReturnsFalse()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);
        _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt);

        // Act
        var updated = _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt.AddSeconds(1));

        // Assert
        Assert.False(updated);
        Assert.Equal(ProcessedAt, _store.GetById(booking.Id)!.ProcessedAt);
    }

    [Fact]
    public void TryUpdateStatus_UnknownId_ReturnsFalse()
    {
        // Act
        var updated = _store.TryUpdateStatus(
            Guid.NewGuid(),
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            ProcessedAt);

        // Assert
        Assert.False(updated);
    }

    [Fact]
    public void TryUpdateStatus_InvalidTargetStatus_Throws()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Pending,
            ProcessedAt));
    }
}
