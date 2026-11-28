using MiddleCsharp.Domain.Enums;

namespace MiddleCsharp.Application.DTOs;

public class BookingInfo
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public BookingStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
}
