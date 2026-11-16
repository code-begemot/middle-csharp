using MiddleCsharp.Models;

namespace MiddleCsharp.Tests.Models;

public class BookingTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ProcessedAt = new(2025, 1, 10, 10, 0, 5, DateTimeKind.Utc);

    [Fact]
    public void CreatePending_InitializesPendingBooking()
    {
        // Act
        var booking = Booking.CreatePending(EventId, CreatedAt);

        // Assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(EventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(CreatedAt, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public void CreatePending_EmptyEventId_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Booking.CreatePending(Guid.Empty, CreatedAt));
    }

    [Fact]
    public void Confirm_SetsStatusAndProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);

        // Act
        booking.Confirm(ProcessedAt);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public void Reject_SetsStatusAndProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);

        // Act
        booking.Reject(ProcessedAt);

        // Assert
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(ProcessedAt, booking.ProcessedAt);
    }

    [Fact]
    public void Confirm_WhenAlreadyProcessed_Throws()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        booking.Confirm(ProcessedAt);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => booking.Confirm(ProcessedAt));
    }

    [Fact]
    public void CreatePending_DefaultCreatedAt_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Booking.CreatePending(EventId, default));
    }

    [Fact]
    public void Confirm_DefaultProcessedAt_Throws()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => booking.Confirm(default));
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public void Reject_AfterConfirmation_Throws()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        booking.Confirm(ProcessedAt);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => booking.Reject(ProcessedAt));
    }
}
