using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Middleware;

namespace MiddleCsharp.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static readonly Guid EventId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400ProblemDetails()
    {
        // Arrange
        var exception = new EventValidationException("Title is required.");

        // Act
        var (context, problem) = await InvokeAsync(exception);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType!);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Equal(exception.Message, problem.Detail);
        Assert.Equal($"/events/{EventId}", problem.Instance);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404ProblemDetails()
    {
        // Arrange
        var exception = new EventNotFoundException(EventId);

        // Act
        var (context, problem) = await InvokeAsync(exception);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType!);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Resource not found", problem.Title);
        Assert.Equal(exception.Message, problem.Detail);
        Assert.Equal($"/events/{EventId}", problem.Instance);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem.Type);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_Returns500WithoutInternalDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("database password=secret");

        // Act
        var (context, problem) = await InvokeAsync(exception);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType!);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("Internal server error", problem.Title);
        Assert.Equal("An unexpected error occurred while processing the request.", problem.Detail);
        Assert.DoesNotContain("secret", problem.Detail);
        Assert.Equal($"/events/{EventId}", problem.Instance);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.6.1", problem.Type);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private static async Task<(HttpContext Context, ProblemDetails Problem)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = HttpMethods.Get,
                Path = $"/events/{EventId}"
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };

        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(problem);
        return (context, problem);
    }
}
