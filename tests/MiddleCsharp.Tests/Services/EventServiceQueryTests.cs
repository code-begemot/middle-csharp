using MiddleCsharp.Exceptions;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.Services;

public class EventServiceQueryTests
{
    private static readonly DateTime Jan10 = new(2025, 1, 10, 10, 0, 0);
    private static readonly DateTime Jan12 = new(2025, 1, 12, 10, 0, 0);
    private static readonly DateTime Jan15 = new(2025, 1, 15, 9, 0, 0);

    private readonly EventService _service = new();

    private void SeedSampleEvents()
    {
        _service.Create(TestData.NewEvent("Team Meeting", Jan10, Jan10.AddHours(1)));
        _service.Create(TestData.NewEvent("team retro", Jan12, Jan12.AddHours(2)));
        _service.Create(TestData.NewEvent("Conference", Jan15, Jan15.AddHours(9)));
    }

    private void SeedFiveEvents()
    {
        for (var i = 1; i <= 5; i++)
        {
            var start = new DateTime(2025, 1, i, 10, 0, 0);
            _service.Create(TestData.NewEvent($"Event {i}", start, start.AddHours(1)));
        }
    }

    [Fact]
    public void FilterByTitle_IsCaseInsensitiveAndPartial()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("TEAM", null, null), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.Contains("team", e.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterByTitle_PartialMatch_ReturnsMatchingEvent()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("meet", null, null), 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Team Meeting", item.Title);
    }

    [Fact]
    public void FilterByTitle_NoMatches_ReturnsEmptyPage()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("unknown", null, null), 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void FilterByTitle_WhitespaceOnly_IsIgnored()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("   ", null, null), 1, 10);

        // Assert
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void FilterByFrom_ReturnsEventsStartingAtOrAfterDate()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter(null, Jan12, null), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.True(e.StartAt >= Jan12));
    }

    [Fact]
    public void FilterByTo_ReturnsEventsEndingAtOrBeforeDate()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter(null, null, Jan12.AddHours(2)), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, e => Assert.True(e.EndAt <= Jan12.AddHours(2)));
    }

    [Fact]
    public void FilterByDateRange_ReturnsEventsInsideRange()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter(null, Jan10.AddDays(1), Jan15.AddHours(9)), 1, 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public void CombinedFilters_TitleAndFrom_AppliesBothConditions()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("team", Jan10.AddDays(1), null), 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("team retro", item.Title);
    }

    [Fact]
    public void CombinedFilters_TitleAndTo_AppliesBothConditions()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(new EventFilter("team", null, Jan10.AddDays(1)), 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Team Meeting", item.Title);
    }

    [Fact]
    public void CombinedFilters_TitleAndDateRange_AppliesAllConditions()
    {
        // Arrange
        SeedSampleEvents();

        // Act
        var result = _service.GetAll(
            new EventFilter("o", Jan10.AddDays(1), Jan15.AddHours(9)),
            1,
            10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, e => e.Title == "Team Meeting");
    }

    [Fact]
    public void FilterByDateRange_FromAfterTo_ThrowsValidation()
    {
        // Arrange
        SeedSampleEvents();

        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(
            () => _service.GetAll(new EventFilter(null, Jan15, Jan10), 1, 10));

        Assert.Equal("From must be earlier than or equal to To.", exception.Message);
    }

    [Fact]
    public void Pagination_FirstPage_ReturnsRequestedSize()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Event 1", result.Items[0].Title);
        Assert.Equal("Event 2", result.Items[1].Title);
    }

    [Fact]
    public void Pagination_SecondPage_ReturnsNextSlice()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 2, 2);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Event 3", result.Items[0].Title);
        Assert.Equal("Event 4", result.Items[1].Title);
    }

    [Fact]
    public void Pagination_LastPage_ReturnsRemainder()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 3, 2);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Event 5", item.Title);
    }

    [Fact]
    public void Pagination_PageBeyondRange_ReturnsEmptyItems()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 10, 2);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(10, result.Page);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Pagination_HugePage_ReturnsEmptyItemsInsteadOfFirstPage()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, int.MaxValue, 100);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Pagination_PageSizeAboveMaximum_ThrowsValidation()
    {
        // Act & Assert
        var exception = Assert.Throws<EventValidationException>(
            () => _service.GetAll(TestData.NoFilter, 1, 101));

        Assert.Equal("PageSize must be between 1 and 100.", exception.Message);
    }

    [Fact]
    public void Pagination_PageSizeAtMaximum_IsAllowed()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 100);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void Pagination_PageSizeLargerThanTotal_ReturnsAllItems()
    {
        // Arrange
        SeedFiveEvents();

        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 50);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void Pagination_OrdersEventsByStartAt()
    {
        // Arrange
        _service.Create(TestData.NewEvent("Later", new DateTime(2025, 3, 1, 10, 0, 0), new DateTime(2025, 3, 1, 11, 0, 0)));
        _service.Create(TestData.NewEvent("Earlier", new DateTime(2025, 2, 1, 10, 0, 0), new DateTime(2025, 2, 1, 11, 0, 0)));

        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal("Earlier", result.Items[0].Title);
        Assert.Equal("Later", result.Items[1].Title);
    }

    [Fact]
    public void Pagination_InvalidPage_ThrowsValidation()
    {
        // Act & Assert
        Assert.Throws<EventValidationException>(() => _service.GetAll(TestData.NoFilter, 0, 10));
    }

    [Fact]
    public void Pagination_InvalidPageSize_ThrowsValidation()
    {
        // Act & Assert
        Assert.Throws<EventValidationException>(() => _service.GetAll(TestData.NoFilter, 1, 0));
    }

    [Fact]
    public void GetAll_EmptyStore_ReturnsEmptyPageWithZeroTotals()
    {
        // Act
        var result = _service.GetAll(TestData.NoFilter, 1, 10);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }
}
