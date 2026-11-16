using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.Exceptions;

namespace MiddleCsharp.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request {Method} {Path} was canceled by the client",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, type, detail) = exception switch
        {
            EventValidationException => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                exception.Message),
            EventNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                exception.Message),
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Payload too large"
                    : "Bad request",
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "https://tools.ietf.org/html/rfc9110#section-15.5.14"
                    : "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Request body is too large."
                    : "The request could not be processed."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                "An unexpected error occurred while processing the request.")
        };

        var unexpected = statusCode == StatusCodes.Status500InternalServerError;

        if (unexpected)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Request {Method} {Path} failed with {StatusCode}: {Message}",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                exception.Message);
        }

        var problem = new ProblemDetails
        {
            Type = type,
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemContentType);
    }
}
