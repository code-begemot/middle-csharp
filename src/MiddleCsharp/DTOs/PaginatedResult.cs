namespace MiddleCsharp.DTOs;

public class PaginatedResult<T>
{
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}
