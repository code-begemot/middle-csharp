using MiddleCsharp.Application.Commands;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Domain.Exceptions;

namespace MiddleCsharp.Tests.Services;

public class EventServiceFailureTests : IDisposable
{
    private readonly TestApplication _app = new();

    private IEventService Service => _app.Get<IEventService>();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task GetById_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var exception = await Assert.ThrowsAsync<EventNotFoundException>(
            () => Service.GetByIdAsync(unknownId));

        // Assert
        Assert.Equal(unknownId, exception.Id);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => Service.UpdateAsync(unknownId, TestData.NewUpdateCommand()));
    }

    [Fact]
    public async Task Delete_UnknownId_ThrowsNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => Service.DeleteAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyTitle_ThrowsValidation(string title)
    {
        // Arrange
        var invalid = TestData.NewCreateCommand(title);

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal("Title is required.", exception.Message);
    }

    [Fact]
    public async Task Create_TitleTooLong_ThrowsValidation()
    {
        // Arrange
        var invalid = TestData.NewCreateCommand(new string('t', EventConstraints.TitleMaxLength + 1));

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal(
            $"Title must be at most {EventConstraints.TitleMaxLength} characters long.",
            exception.Message);
    }

    [Fact]
    public async Task Create_DescriptionTooLong_ThrowsValidation()
    {
        // Arrange
        var invalid = new CreateEventCommand(
            "Meeting",
            new string('d', EventConstraints.DescriptionMaxLength + 1),
            TestData.DefaultStartAt,
            TestData.DefaultEndAt,
            TotalSeats: 10);

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal(
            $"Description must be at most {EventConstraints.DescriptionMaxLength} characters long.",
            exception.Message);
    }

    [Fact]
    public async Task Create_DefaultStartAt_ThrowsValidation()
    {
        // Arrange
        var invalid = new CreateEventCommand(
            "Meeting",
            null,
            default,
            TestData.DefaultEndAt,
            TotalSeats: 10);

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal("StartAt is required.", exception.Message);
    }

    [Fact]
    public async Task Create_DefaultEndAt_ThrowsValidation()
    {
        // Arrange
        var invalid = new CreateEventCommand(
            "Meeting",
            null,
            TestData.DefaultStartAt,
            default,
            TotalSeats: 10);

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal("EndAt is required.", exception.Message);
    }

    [Fact]
    public async Task Create_EndBeforeStart_ThrowsValidation()
    {
        // Arrange
        var invalid = TestData.NewCreateCommand(
            startAt: new DateTime(2025, 1, 10, 11, 0, 0),
            endAt: new DateTime(2025, 1, 10, 10, 0, 0));

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));

        // Assert
        Assert.Equal("EndAt must be later than StartAt.", exception.Message);
    }

    [Fact]
    public async Task Create_EndEqualsStart_ThrowsValidation()
    {
        // Arrange
        var moment = new DateTime(2025, 1, 10, 10, 0, 0);
        var invalid = TestData.NewCreateCommand(startAt: moment, endAt: moment);

        // Act & Assert
        await Assert.ThrowsAsync<EventValidationException>(
            () => Service.CreateAsync(invalid));
    }

    [Fact]
    public async Task Update_EndBeforeStart_ThrowsValidation()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewCreateCommand());
        var invalid = TestData.NewUpdateCommand(
            startAt: new DateTime(2025, 1, 10, 11, 0, 0),
            endAt: new DateTime(2025, 1, 10, 10, 0, 0));

        // Act & Assert
        await Assert.ThrowsAsync<EventValidationException>(
            () => Service.UpdateAsync(created.Id, invalid));
    }

    [Fact]
    public async Task Create_InvalidEvent_DoesNotStoreIt()
    {
        // Arrange
        var invalid = TestData.NewCreateCommand("");

        // Act
        var create = () => Service.CreateAsync(invalid);

        // Assert
        await Assert.ThrowsAsync<EventValidationException>(create);
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 10);
        Assert.Equal(0, result.TotalCount);
    }
}
