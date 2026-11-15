using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.Services;

namespace MiddleCsharp.DTOs;

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
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;

    public EventFilter ToFilter() => new(Title, From, To);
}
