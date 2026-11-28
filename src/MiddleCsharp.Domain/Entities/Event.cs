namespace MiddleCsharp.Domain.Entities;

public class Event
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public int TotalSeats { get; private set; }
    public int AvailableSeats { get; private set; }
    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    private Event()
    {
    }

    public Event(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = description;
        StartAt = NormalizeToUnspecified(startAt);
        EndAt = NormalizeToUnspecified(endAt);
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
    }

    public void UpdateDetails(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt)
    {
        Title = title;
        Description = description;
        StartAt = NormalizeToUnspecified(startAt);
        EndAt = NormalizeToUnspecified(endAt);
    }

    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        if (AvailableSeats < count)
        {
            return false;
        }

        AvailableSeats -= count;
        return true;
    }

    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        AvailableSeats = (int)Math.Min(TotalSeats, (long)AvailableSeats + count);
    }

    private static DateTime NormalizeToUnspecified(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
