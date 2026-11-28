using MiddleCsharp.Application.DTOs;

namespace MiddleCsharp.Tests.DTOs;

public sealed class PaginatedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(20, 10, 2)]
    [InlineData(21, 10, 3)]
    [InlineData(5, 2, 3)]
    public void TotalPages_IsCalculatedFromTotalCountAndPageSize(
        int totalCount,
        int pageSize,
        int expected)
    {
        // Arrange
        var result = new PaginatedResult<int>
        {
            TotalCount = totalCount,
            Page = 1,
            PageSize = pageSize
        };

        // Act
        var totalPages = result.TotalPages;

        // Assert
        Assert.Equal(expected, totalPages);
    }

    [Fact]
    public void TotalPages_ZeroPageSize_ReturnsZero()
    {
        // Arrange
        var result = new PaginatedResult<int>
        {
            TotalCount = 10,
            Page = 1,
            PageSize = 0
        };

        // Act
        var totalPages = result.TotalPages;

        // Assert
        Assert.Equal(0, totalPages);
    }
}
