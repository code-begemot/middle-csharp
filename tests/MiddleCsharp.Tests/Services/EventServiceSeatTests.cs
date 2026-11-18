using MiddleCsharp.Exceptions;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceSeatTests
{
    private readonly EventService _service = new();

    [Fact]
    public void Create_SetsAvailableSeatsToTotalSeats()
    {
        // Act
        var created = _service.Create(TestData.NewEvent(totalSeats: 5));

        // Assert
        Assert.Equal(5, created.TotalSeats);
        Assert.Equal(5, created.AvailableSeats);
        Assert.Equal(5, _service.GetById(created.Id).AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
    {
        // Act & Assert
        Assert.Throws<EventValidationException>(
            () => _service.Create(TestData.NewEvent(totalSeats: totalSeats)));
    }

    [Fact]
    public void Update_PreservesSeats()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent(totalSeats: 5));
        _service.TryReserveSeats(created.Id, 2);

        // Act
        _service.Update(created.Id, TestData.NewEvent("Updated", totalSeats: 100));

        // Assert
        var stored = _service.GetById(created.Id);
        Assert.Equal(5, stored.TotalSeats);
        Assert.Equal(3, stored.AvailableSeats);
        Assert.Equal("Updated", stored.Title);
    }

    [Fact]
    public void TryReserveSeats_SeatsAvailable_DecrementsStoredEvent()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent(totalSeats: 3));

        // Act
        var reserved = _service.TryReserveSeats(created.Id);

        // Assert
        Assert.True(reserved);
        Assert.Equal(2, _service.GetById(created.Id).AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_NoSeatsLeft_ReturnsFalse()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent(totalSeats: 1));
        _service.TryReserveSeats(created.Id);

        // Act
        var reserved = _service.TryReserveSeats(created.Id);

        // Assert
        Assert.False(reserved);
        Assert.Equal(0, _service.GetById(created.Id).AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_UnknownEvent_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<EventNotFoundException>(
            () => _service.TryReserveSeats(unknownId));

        Assert.Equal(unknownId, exception.Id);
    }

    [Fact]
    public void ReleaseSeats_RestoresStoredEvent()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent(totalSeats: 3));
        _service.TryReserveSeats(created.Id);
        _service.TryReserveSeats(created.Id);

        // Act
        _service.ReleaseSeats(created.Id);

        // Assert
        Assert.Equal(2, _service.GetById(created.Id).AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_UnknownEvent_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<EventNotFoundException>(
            () => _service.ReleaseSeats(unknownId));

        Assert.Equal(unknownId, exception.Id);
    }

    [Fact]
    public async Task TryReserveSeats_ConcurrentRequests_NeverExceedsCapacity()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent(totalSeats: 5));

        // Act
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => _service.TryReserveSeats(created.Id)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Count(reserved => reserved));
        Assert.Equal(0, _service.GetById(created.Id).AvailableSeats);
    }
}
