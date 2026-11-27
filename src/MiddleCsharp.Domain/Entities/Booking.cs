using MiddleCsharp.Domain.Enums;

namespace MiddleCsharp.Domain.Entities;

public class Booking
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Event Event { get; private set; } = null!;
    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private Booking()
    {
    }

    public static Booking CreatePending(Guid eventId, DateTime createdAt)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("EventId is required.", nameof(eventId));
        }

        if (createdAt == default)
        {
            throw new ArgumentException("CreatedAt is required.", nameof(createdAt));
        }

        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Pending,
            CreatedAt = createdAt
        };
    }

    public void Confirm(DateTime processedAt) => Complete(BookingStatus.Confirmed, processedAt);

    public void Reject(DateTime processedAt) => Complete(BookingStatus.Rejected, processedAt);

    private void Complete(BookingStatus status, DateTime processedAt)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Booking {Id} is already processed with status {Status}.");
        }

        if (processedAt == default)
        {
            throw new ArgumentException("ProcessedAt is required.", nameof(processedAt));
        }

        Status = status;
        ProcessedAt = processedAt;
    }
}
