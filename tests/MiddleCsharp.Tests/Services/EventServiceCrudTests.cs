using MiddleCsharp.Exceptions;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceCrudTests
{
    private readonly EventService _service = new();

    [Fact]
    public void Create_ValidEvent_StoresEventAndAssignsId()
    {
        // Act
        var created = _service.Create(TestData.NewEvent());

        // Assert
        Assert.NotEqual(Guid.Empty, created.Id);
        var stored = _service.GetById(created.Id);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Meeting", stored.Title);
        Assert.Equal("Sprint planning", stored.Description);
    }

    [Fact]
    public void Create_ClientProvidedId_IsIgnored()
    {
        // Arrange
        var clientId = Guid.NewGuid();

        // Act
        var created = _service.Create(TestData.NewEvent(id: clientId));

        // Assert
        Assert.NotEqual(clientId, created.Id);
        Assert.Single(_service.GetAll(TestData.NoFilter, 1, 10).Items);
    }

    [Fact]
    public void GetAll_ReturnsAllStoredEvents()
    {
        // Arrange
        _service.Create(TestData.NewEvent("First"));
        _service.Create(TestData.NewEvent("Second"));
        _service.Create(TestData.NewEvent("Third"));

        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public void GetById_ExistingEvent_ReturnsEvent()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent("Standup"));

        // Act
        var found = _service.GetById(created.Id);

        // Assert
        Assert.Equal("Standup", found.Title);
    }

    [Fact]
    public void Update_ExistingEvent_ReplacesAllFields()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent());
        var updatedStart = new DateTime(2025, 2, 1, 9, 0, 0);
        var updatedEnd = new DateTime(2025, 2, 1, 10, 0, 0);

        // Act
        _service.Update(created.Id, TestData.NewEvent("Updated", updatedStart, updatedEnd));

        // Assert
        var stored = _service.GetById(created.Id);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Updated", stored.Title);
        Assert.Equal("Sprint planning", stored.Description);
        Assert.Equal(updatedStart, stored.StartAt);
        Assert.Equal(updatedEnd, stored.EndAt);
    }

    [Fact]
    public void Delete_ExistingEvent_RemovesEvent()
    {
        // Arrange
        var created = _service.Create(TestData.NewEvent());

        // Act
        _service.Delete(created.Id);

        // Assert
        Assert.Throws<EventNotFoundException>(() => _service.GetById(created.Id));
        Assert.Equal(0, _service.GetAll(TestData.NoFilter, 1, 10).TotalCount);
    }
}
