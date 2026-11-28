using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using MiddleCsharp.Presentation.Filters;

namespace MiddleCsharp.Presentation.Tests.Filters;

public class ProblemDetailsContentTypeFilterTests
{
    private readonly ProblemDetailsContentTypeFilter _filter = new();

    [Fact]
    public void OnResultExecuting_ProblemDetailsResult_SetsProblemJsonContentType()
    {
        // Arrange
        var result = new ObjectResult(new ProblemDetails { Status = StatusCodes.Status415UnsupportedMediaType })
        {
            StatusCode = StatusCodes.Status415UnsupportedMediaType
        };
        var context = CreateContext(result);

        // Act
        _filter.OnResultExecuting(context);

        // Assert
        Assert.Equal(
            new[] { ProblemDetailsContentTypeFilter.ProblemDetailsContentType },
            result.ContentTypes.ToArray());
    }

    [Fact]
    public void OnResultExecuting_NonProblemDetailsResult_KeepsContentTypes()
    {
        // Arrange
        var result = new ObjectResult(new { value = 1 }) { StatusCode = StatusCodes.Status200OK };
        var context = CreateContext(result);

        // Act
        _filter.OnResultExecuting(context);

        // Assert
        Assert.Empty(result.ContentTypes);
    }

    private static ResultExecutingContext CreateContext(IActionResult result)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ResultExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            result,
            new object());
    }
}
