using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MiddleCsharp.Presentation.Filters;

public sealed class ProblemDetailsContentTypeFilter : IAlwaysRunResultFilter, IOrderedFilter
{
    public const string ProblemDetailsContentType = "application/problem+json";

    private const int FilterOrder = -1900;

    public int Order => FilterOrder;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult result || result.Value is not ProblemDetails)
        {
            return;
        }

        result.ContentTypes.Clear();
        result.ContentTypes.Add(ProblemDetailsContentType);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
