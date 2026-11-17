using MiddleCsharp.Models;

namespace MiddleCsharp.Tests.Models;

public class EventTests
{
    [Fact]
    public void TryReserveSeats_SeatsAvailable_DecrementsAndReturnsTrue()
    {
        // Arrange
        var evt = new Event { TotalSeats = 3, AvailableSeats = 3 };

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
        var evt = new Event { TotalSeats = 1, AvailableSeats = 0 };

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
        var evt = new Event { TotalSeats = 5, AvailableSeats = 2 };

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
        var evt = new Event { TotalSeats = 5, AvailableSeats = 5 };

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
        var evt = new Event { TotalSeats = 5, AvailableSeats = 5 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => evt.TryReserveSeats(count));
    }

    [Fact]
    public void ReleaseSeats_ReturnsSeatToPool()
    {
        // Arrange
        var evt = new Event { TotalSeats = 3, AvailableSeats = 1 };

        // Act
        evt.ReleaseSeats();

        // Assert
        Assert.Equal(2, evt.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_NeverExceedsTotalSeats()
    {
        // Arrange
        var evt = new Event { TotalSeats = 3, AvailableSeats = 3 };

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
        var evt = new Event { TotalSeats = 5, AvailableSeats = 5 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => evt.ReleaseSeats(count));
    }

    [Fact]
    public void Clone_CopiesSeats()
    {
        // Arrange
        var evt = new Event { TotalSeats = 7, AvailableSeats = 4 };

        // Act
        var clone = evt.Clone();

        // Assert
        Assert.Equal(7, clone.TotalSeats);
        Assert.Equal(4, clone.AvailableSeats);
    }
}
