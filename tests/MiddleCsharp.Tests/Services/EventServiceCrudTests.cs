using MiddleCsharp.DataAccess;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceCrudTests : IDisposable
{
    private readonly TestApplication _app = new();

    private IEventService Service => _app.Get<IEventService>();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Create_ValidEvent_StoresEventAndAssignsId()
    {
        // Act
        var created = await Service.CreateAsync(TestData.NewEvent());

        // Assert
        Assert.NotEqual(Guid.Empty, created.Id);
        var stored = await Service.GetByIdAsync(created.Id);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Meeting", stored.Title);
        Assert.Equal("Sprint planning", stored.Description);
    }

    [Fact]
    public async Task GetAll_ReturnsAllStoredEvents()
    {
        // Arrange
        await Service.CreateAsync(TestData.NewEvent("First"));
        await Service.CreateAsync(TestData.NewEvent("Second"));
        await Service.CreateAsync(TestData.NewEvent("Third"));

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task GetById_ExistingEvent_ReturnsEvent()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewEvent("Standup"));

        // Act
        var found = await Service.GetByIdAsync(created.Id);

        // Assert
        Assert.Equal("Standup", found.Title);
    }

    [Fact]
    public async Task GetById_ReturnedEvent_IsNotTracked()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewEvent("Original"));
        var fetched = await Service.GetByIdAsync(created.Id);

        // Act
        fetched.UpdateDetails(
            "Mutated",
            fetched.Description,
            fetched.StartAt,
            fetched.EndAt);

        var context = _app.Get<AppDbContext>();
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal("Original", (await Service.GetByIdAsync(created.Id)).Title);
    }

    [Fact]
    public async Task GetAll_ReturnedItems_AreNotTracked()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewEvent("Original"));
        var item = Assert.Single((await Service.GetAllAsync(TestData.NoFilter, 1, 10)).Items);

        // Act
        item.UpdateDetails(
            "Mutated",
            item.Description,
            item.StartAt,
            item.EndAt);

        var context = _app.Get<AppDbContext>();
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal("Original", (await Service.GetByIdAsync(created.Id)).Title);
    }

    [Fact]
    public async Task Update_ExistingEvent_ReplacesAllFields()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewEvent());
        var updatedStart = new DateTime(2025, 2, 1, 9, 0, 0);
        var updatedEnd = new DateTime(2025, 2, 1, 10, 0, 0);

        // Act
        await Service.UpdateAsync(
            created.Id,
            TestData.NewEvent("Updated", updatedStart, updatedEnd));

        // Assert
        var stored = await Service.GetByIdAsync(created.Id);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal("Updated", stored.Title);
        Assert.Equal("Sprint planning", stored.Description);
        Assert.Equal(updatedStart, stored.StartAt);
        Assert.Equal(updatedEnd, stored.EndAt);
    }

    [Fact]
    public async Task Delete_ExistingEvent_RemovesEvent()
    {
        // Arrange
        var created = await Service.CreateAsync(TestData.NewEvent());

        // Act
        await Service.DeleteAsync(created.Id);

        // Assert
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => Service.GetByIdAsync(created.Id));

        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 10);
        Assert.Equal(0, result.TotalCount);
    }
}
