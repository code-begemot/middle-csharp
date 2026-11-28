using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.Application.DTOs;

namespace MiddleCsharp.Presentation.DTOs;

public class GetEventsQuery
{
    [FromQuery(Name = "title")]
    public string? Title { get; set; }

    [FromQuery(Name = "from")]
    public DateTime? From { get; set; }

    [FromQuery(Name = "to")]
    public DateTime? To { get; set; }

    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
    public int Page { get; set; } = PaginationLimits.DefaultPage;

    [FromQuery(Name = "pageSize")]
    [Range(1, PaginationLimits.MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = PaginationLimits.DefaultPageSize;

    public EventFilter ToFilter() => new(Title, From, To);
}
