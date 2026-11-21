using MiddleCsharp.Models;

namespace MiddleCsharp.Tests.Models;

public class EventTests
{
    private static readonly DateTime StartAt = new(2025, 1, 10, 10, 0, 0);
    private static readonly DateTime EndAt = new(2025, 1, 10, 11, 0, 0);

    private static Event CreateEvent(int totalSeats = 5) =>
        new("Meeting", "Sprint planning", StartAt, EndAt, totalSeats);

    [Fact]
    public void Constructor_InitializesSeatsAndId()
    {
        // Act
        var evt = CreateEvent(totalSeats: 7);

        // Assert
        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal(7, evt.TotalSeats);
        Assert.Equal(7, evt.AvailableSeats);
    }

    [Fact]
    public void Constructor_NormalizesDatesToUnspecified()
    {
        // Arrange
        var utcStart = new DateTime(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        var utcEnd = new DateTime(2025, 1, 10, 11, 0, 0, DateTimeKind.Utc);

        // Act
        var evt = new Event("Meeting", null, utcStart, utcEnd, totalSeats: 5);

        // Assert
        Assert.Equal(DateTimeKind.Unspecified, evt.StartAt.Kind);
        Assert.Equal(DateTimeKind.Unspecified, evt.EndAt.Kind);
        Assert.Equal(utcStart.Ticks, evt.StartAt.Ticks);
        Assert.Equal(utcEnd.Ticks, evt.EndAt.Ticks);
    }

    [Fact]
    public void TryReserveSeats_SeatsAvailable_DecrementsAndReturnsTrue()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 3);

        // Act
        var reserved = evt.TryReserveSeats();

        // Assert
        Assert.True(reserved);
        Assert.Equal(2, evt.AvailableSeats);
        Assert.Equal(3, evt.TotalSeats);
    }

    [Fact]
    public void TryReserveSeats_NotEnoughSeats_ReturnsFalseAndKeepsSeats()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 1);
        evt.TryReserveSeats();

        // Act
        var reserved = evt.TryReserveSeats();

        // Assert
        Assert.False(reserved);
        Assert.Equal(0, evt.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_CountGreaterThanAvailable_ReturnsFalse()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 5);
        evt.TryReserveSeats(3);

        // Act
        var reserved = evt.TryReserveSeats(3);

        // Assert
        Assert.False(reserved);
        Assert.Equal(2, evt.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_MultipleSeats_DecrementsByCount()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 5);

        // Act
        var reserved = evt.TryReserveSeats(2);

        // Assert
        Assert.True(reserved);
        Assert.Equal(3, evt.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryReserveSeats_NonPositiveCount_Throws(int count)
    {
        // Arrange
        var evt = CreateEvent();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => evt.TryReserveSeats(count));
    }

    [Fact]
    public void ReleaseSeats_ReturnsSeatToPool()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 3);
        evt.TryReserveSeats(2);

        // Act
        evt.ReleaseSeats();

        // Assert
        Assert.Equal(2, evt.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_NeverExceedsTotalSeats()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 3);

        // Act
        evt.ReleaseSeats();

        // Assert
        Assert.Equal(3, evt.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseSeats_NonPositiveCount_Throws(int count)
    {
        // Arrange
        var evt = CreateEvent();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => evt.ReleaseSeats(count));
    }

    [Fact]
    public void UpdateDetails_ChangesFieldsAndPreservesSeats()
    {
        // Arrange
        var evt = CreateEvent(totalSeats: 5);
        evt.TryReserveSeats(2);
        var updatedStart = new DateTime(2025, 2, 1, 9, 0, 0);
        var updatedEnd = new DateTime(2025, 2, 1, 10, 0, 0);

        // Act
        evt.UpdateDetails("Updated", "New description", updatedStart, updatedEnd);

        // Assert
        Assert.Equal("Updated", evt.Title);
        Assert.Equal("New description", evt.Description);
        Assert.Equal(updatedStart, evt.StartAt);
        Assert.Equal(updatedEnd, evt.EndAt);
        Assert.Equal(5, evt.TotalSeats);
        Assert.Equal(3, evt.AvailableSeats);
    }
}
