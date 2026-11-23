using Microsoft.EntityFrameworkCore;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class EventRepositoryTests : RepositoryTestBase
{
    public EventRepositoryTests(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsStoredEvent()
    {
        // Arrange
        var start = new DateTime(2025, 1, 10, 10, 0, 0);
        var newEvent = new Event("Meeting", "Sprint planning", start, start.AddHours(1), 5);

        // Act
        await Events.AddAsync(newEvent);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var stored = await Events.GetByIdAsync(newEvent.Id);
        Assert.NotNull(stored);
        Assert.Equal("Meeting", stored.Title);
        Assert.Equal("Sprint planning", stored.Description);
        Assert.Equal(start, stored.StartAt);
        Assert.Equal(start.AddHours(1), stored.EndAt);
        Assert.Equal(5, stored.TotalSeats);
        Assert.Equal(5, stored.AvailableSeats);
        Assert.Equal(DateTimeKind.Unspecified, stored.StartAt.Kind);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        // Act
        var stored = await Events.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(stored);
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotTrackEntity()
    {
        // Arrange
        var created = await AddEventAsync();

        var fetched = await Events.GetByIdAsync(created.Id);
        Assert.NotNull(fetched);

        // Act
        fetched.UpdateDetails(
            "Mutated",
            fetched.Description,
            fetched.StartAt,
            fetched.EndAt);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var stored = await context.Events
            .AsNoTracking()
            .SingleAsync(evt => evt.Id == created.Id);
        Assert.Equal("Meeting", stored.Title);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_PersistsChanges()
    {
        // Arrange
        var created = await AddEventAsync(totalSeats: 5);
        var updatedStart = new DateTime(2025, 2, 1, 9, 0, 0);
        var updatedEnd = new DateTime(2025, 2, 1, 10, 0, 0);

        // Act
        var stored = await Events.GetByIdForUpdateAsync(created.Id);
        Assert.NotNull(stored);
        stored.UpdateDetails("Updated", "Updated description", updatedStart, updatedEnd);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        await using var context = CreateContext();
        var updated = await context.Events
            .AsNoTracking()
            .SingleAsync(evt => evt.Id == created.Id);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(updatedStart, updated.StartAt);
        Assert.Equal(updatedEnd, updated.EndAt);
        Assert.Equal(5, updated.TotalSeats);
        Assert.Equal(5, updated.AvailableSeats);
    }

    [Fact]
    public async Task GetPageAsync_NoFilter_ReturnsOrderedPage()
    {
        // Arrange
        await AddEventAsync("Later", new DateTime(2025, 3, 1, 10, 0, 0));
        await AddEventAsync("Earlier", new DateTime(2025, 2, 1, 10, 0, 0));
        await AddEventAsync("Middle", new DateTime(2025, 2, 15, 10, 0, 0));

        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, null), 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(new[] { "Earlier", "Middle", "Later" }, result.Items.Select(evt => evt.Title));
    }

    [Fact]
    public async Task GetPageAsync_FilterByTitle_IsCaseInsensitiveAndPartial()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter("TEAM", null, null), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(
            result.Items,
            evt => Assert.Contains("team", evt.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPageAsync_FilterByTitle_WhitespaceOnly_IsIgnored()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter("   ", null, null), 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetPageAsync_FilterByFrom_IsInclusive()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Events.GetPageAsync(
            new EventFilter(null, new DateTime(2025, 1, 12, 10, 0, 0), null),
            1,
            10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, evt => Assert.True(evt.StartAt >= new DateTime(2025, 1, 12, 10, 0, 0)));
    }

    [Fact]
    public async Task GetPageAsync_FilterByTo_IsInclusive()
    {
        // Arrange
        await SeedSampleEventsAsync();
        var to = new DateTime(2025, 1, 12, 12, 0, 0);

        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, to), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, evt => Assert.True(evt.EndAt <= to));
    }

    [Fact]
    public async Task GetPageAsync_CombinedFilters_AppliesAllConditions()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Events.GetPageAsync(
            new EventFilter("team", new DateTime(2025, 1, 11, 0, 0, 0), null),
            1,
            10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("team retro", item.Title);
    }

    [Fact]
    public async Task GetPageAsync_FilterNoMatches_ReturnsEmptyPage()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter("unknown", null, null), 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetPageAsync_SecondPage_ReturnsRequestedSlice()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, null), 2, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Event 3", result.Items[0].Title);
        Assert.Equal("Event 4", result.Items[1].Title);
    }

    [Fact]
    public async Task GetPageAsync_FirstAndLastPage_ReturnExpectedSlices()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var first = await Events.GetPageAsync(new EventFilter(null, null, null), 1, 2);
        var last = await Events.GetPageAsync(new EventFilter(null, null, null), 3, 2);

        // Assert
        Assert.Equal(new[] { "Event 1", "Event 2" }, first.Items.Select(evt => evt.Title));
        Assert.Equal(new[] { "Event 5" }, last.Items.Select(evt => evt.Title));
    }

    [Fact]
    public async Task GetPageAsync_PageBeyondRange_ReturnsEmptyItems()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, null), 10, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(10, result.Page);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetPageAsync_PageSizeLargerThanTotal_ReturnsAllItems()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, null), 1, 50);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetPageAsync_EmptyDatabase_ReturnsEmptyPage()
    {
        // Act
        var result = await Events.GetPageAsync(new EventFilter(null, null, null), 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task RemoveAsync_ExistingEvent_RemovesIt()
    {
        // Arrange
        var created = await AddEventAsync();

        // Act
        var removed = await Events.RemoveAsync(created.Id);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        Assert.True(removed);
        Assert.Null(await Events.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task RemoveAsync_UnknownId_ReturnsFalse()
    {
        // Act
        var removed = await Events.RemoveAsync(Guid.NewGuid());

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveAsync_EventWithBookings_RemovesBookings()
    {
        // Arrange
        var created = await AddEventAsync(totalSeats: 2);
        var booking = await AddPendingBookingAsync(created.Id);

        // Act
        var removed = await Events.RemoveAsync(created.Id);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        Assert.True(removed);
        await using var context = CreateContext();
        Assert.Null(await context.Events.SingleOrDefaultAsync(evt => evt.Id == created.Id));
        Assert.Null(await context.Bookings.SingleOrDefaultAsync(stored => stored.Id == booking.Id));
    }

    private async Task SeedSampleEventsAsync()
    {
        await AddEventAsync("Team Meeting", new DateTime(2025, 1, 10, 10, 0, 0));
        await AddEventAsync("team retro", new DateTime(2025, 1, 12, 10, 0, 0));
        await AddEventAsync("Conference", new DateTime(2025, 1, 15, 9, 0, 0));
    }

    private async Task SeedFiveEventsAsync()
    {
        for (var i = 1; i <= 5; i++)
        {
            await AddEventAsync(
                $"Event {i}",
                new DateTime(2025, 1, i, 10, 0, 0));
        }
    }
}
