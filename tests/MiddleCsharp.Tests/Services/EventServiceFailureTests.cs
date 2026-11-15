using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceFailureTests
{
    private readonly EventService _service = new();

    [Fact]
    public void GetById_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<EventNotFoundException>(() => _service.GetById(unknownId));

        // Assert
        Assert.Equal(unknownId, exception.Id);
    }

    [Fact]
    public void Update_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<EventNotFoundException>(() => _service.Update(unknownId, TestData.NewEvent()));
    }

    [Fact]
    public void Delete_UnknownId_ThrowsNotFound()
    {
        // Act & Assert
        Assert.Throws<EventNotFoundException>(() => _service.Delete(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyTitle_ThrowsValidation(string title)
    {
        // Arrange
        var invalid = TestData.NewEvent(title);

        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(() => _service.Create(invalid));
        Assert.Equal("Title is required.", exception.Message);
    }

    [Fact]
    public void Create_DefaultStartAt_ThrowsValidation()
    {
        // Arrange
        var invalid = new Event
        {
            Title = "Meeting",
            StartAt = default,
            EndAt = TestData.DefaultEndAt
        };

        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(() => _service.Create(invalid));
        Assert.Equal("StartAt is required.", exception.Message);
    }

    [Fact]
    public void Create_DefaultEndAt_ThrowsValidation()
    {
        // Arrange
        var invalid = new Event
        {
            Title = "Meeting",
            StartAt = TestData.DefaultStartAt,
            EndAt = default
        };

        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(() => _service.Create(invalid));
        Assert.Equal("EndAt is required.", exception.Message);
    }

    [Fact]
    public void Create_EndBeforeStart_ThrowsValidation()
    {
        // Arrange
        var invalid = TestData.NewEvent(
            startAt: new DateTime(2025, 1, 10, 11, 0, 0),
            endAt: new DateTime(2025, 1, 10, 10, 0, 0));

        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(() => _service.Create(invalid));
        Assert.Equal("EndAt must be later than StartAt.", exception.Message);
    }

    [Fact]
    public void Create_EndEqualsStart_ThrowsValidation()
    {
        // Arrange
        var moment = new DateTime(2025, 1, 10, 10, 0, 0);
        var invalid = TestData.NewEvent(startAt: moment, endAt: moment);

        // Act & Assert
        Assert.Throws<EventValidationException>(() => _service.Create(invalid));
    }

    [Fact]
    public void Update_EndBeforeStart_ThrowsValidation()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent());
        var invalid = TestData.NewEvent(
            startAt: new DateTime(2025, 1, 10, 11, 0, 0),
            endAt: new DateTime(2025, 1, 10, 10, 0, 0));

        // Act & Assert
        Assert.Throws<EventValidationException>(() => _service.Update(created.Id, invalid));
    }

    [Fact]
    public void Create_InvalidEvent_DoesNotStoreIt()
    {
        // Arrange
        var invalid = TestData.NewEvent("");

        // Act
        Assert.Throws<EventValidationException>(() => _service.Create(invalid));

        // Assert
        Assert.Equal(0, _service.GetAll(TestData.NoFilter, 1, 10).TotalCount);
    }
}
