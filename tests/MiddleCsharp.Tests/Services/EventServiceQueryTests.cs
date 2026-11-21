using MiddleCsharp.Exceptions;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceQueryTests : IDisposable
{
    private static readonly DateTime Jan10 = new(2025, 1, 10, 10, 0, 0);
    private static readonly DateTime Jan12 = new(2025, 1, 12, 10, 0, 0);
    private static readonly DateTime Jan15 = new(2025, 1, 15, 9, 0, 0);

    private readonly TestApplication _app = new();

    private IEventService Service => _app.Get<IEventService>();

    public void Dispose() => _app.Dispose();

    private async Task SeedSampleEventsAsync()
    {
        await Service.CreateAsync(TestData.NewEvent("Team Meeting", Jan10, Jan10.AddHours(1)));
        await Service.CreateAsync(TestData.NewEvent("team retro", Jan12, Jan12.AddHours(2)));
        await Service.CreateAsync(TestData.NewEvent("Conference", Jan15, Jan15.AddHours(9)));
    }

    private async Task SeedFiveEventsAsync()
    {
        for (var i = 1; i <= 5; i++)
        {
            var start = new DateTime(2025, 1, i, 10, 0, 0);
            await Service.CreateAsync(TestData.NewEvent($"Event {i}", start, start.AddHours(1)));
        }
    }

    [Fact]
    public async Task FilterByTitle_IsCaseInsensitiveAndPartial()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter("TEAM", null, null), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.Contains("team", e.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FilterByTitle_PartialMatch_ReturnsMatchingEvent()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter("meet", null, null), 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Team Meeting", item.Title);
    }

    [Fact]
    public async Task FilterByTitle_NoMatches_ReturnsEmptyPage()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter("unknown", null, null), 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task FilterByTitle_WhitespaceOnly_IsIgnored()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter("   ", null, null), 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task FilterByFrom_ReturnsEventsStartingAtOrAfterDate()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter(null, Jan12, null), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.True(e.StartAt >= Jan12));
    }

    [Fact]
    public async Task FilterByTo_ReturnsEventsEndingAtOrBeforeDate()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(new EventFilter(null, null, Jan12.AddHours(2)), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.True(e.EndAt <= Jan12.AddHours(2)));
    }

    [Fact]
    public async Task FilterByDateRange_ReturnsEventsInsideRange()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(
            new EventFilter(null, Jan10.AddDays(1), Jan15.AddHours(9)),
            1,
            10);

        // Assert
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task CombinedFilters_TitleAndFrom_AppliesBothConditions()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(
            new EventFilter("team", Jan10.AddDays(1), null),
            1,
            10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("team retro", item.Title);
    }

    [Fact]
    public async Task CombinedFilters_TitleAndTo_AppliesBothConditions()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(
            new EventFilter("team", null, Jan10.AddDays(1)),
            1,
            10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Team Meeting", item.Title);
    }

    [Fact]
    public async Task CombinedFilters_TitleAndDateRange_AppliesAllConditions()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var result = await Service.GetAllAsync(
            new EventFilter("o", Jan10.AddDays(1), Jan15.AddHours(9)),
            1,
            10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, e => e.Title == "Team Meeting");
    }

    [Fact]
    public async Task FilterByDateRange_FromAfterTo_ThrowsValidation()
    {
        // Arrange
        await SeedSampleEventsAsync();

        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.GetAllAsync(new EventFilter(null, Jan15, Jan10), 1, 10));

        // Assert
        Assert.Equal("From must be earlier than or equal to To.", exception.Message);
    }

    [Fact]
    public async Task Pagination_FirstPage_ReturnsRequestedSize()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Event 1", result.Items[0].Title);
        Assert.Equal("Event 2", result.Items[1].Title);
    }

    [Fact]
    public async Task Pagination_SecondPage_ReturnsNextSlice()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 2, 2);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Event 3", result.Items[0].Title);
        Assert.Equal("Event 4", result.Items[1].Title);
    }

    [Fact]
    public async Task Pagination_LastPage_ReturnsRemainder()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 3, 2);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Event 5", item.Title);
    }

    [Fact]
    public async Task Pagination_PageBeyondRange_ReturnsEmptyItems()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 10, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(10, result.Page);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Pagination_HugePage_ReturnsEmptyItemsInsteadOfFirstPage()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, int.MaxValue, 100);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Pagination_PageSizeAboveMaximum_ThrowsValidation()
    {
        // Act
        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () => Service.GetAllAsync(TestData.NoFilter, 1, 101));

        // Assert
        Assert.Equal("PageSize must be between 1 and 100.", exception.Message);
    }

    [Fact]
    public async Task Pagination_PageSizeAtMaximum_IsAllowed()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 100);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task Pagination_PageSizeLargerThanTotal_ReturnsAllItems()
    {
        // Arrange
        await SeedFiveEventsAsync();

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 50);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task Pagination_OrdersEventsByStartAt()
    {
        // Arrange
        await Service.CreateAsync(TestData.NewEvent(
            "Later",
            new DateTime(2025, 3, 1, 10, 0, 0),
            new DateTime(2025, 3, 1, 11, 0, 0)));

        await Service.CreateAsync(TestData.NewEvent(
            "Earlier",
            new DateTime(2025, 2, 1, 10, 0, 0),
            new DateTime(2025, 2, 1, 11, 0, 0)));

        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal("Earlier", result.Items[0].Title);
        Assert.Equal("Later", result.Items[1].Title);
    }

    [Fact]
    public async Task Pagination_InvalidPage_ThrowsValidation()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EventValidationException>(
            () => Service.GetAllAsync(TestData.NoFilter, 0, 10));
    }

    [Fact]
    public async Task Pagination_InvalidPageSize_ThrowsValidation()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EventValidationException>(
            () => Service.GetAllAsync(TestData.NoFilter, 1, 0));
    }

    [Fact]
    public async Task GetAll_EmptyStore_ReturnsEmptyPageWithZeroTotals()
    {
        // Act
        var result = await Service.GetAllAsync(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }
}
