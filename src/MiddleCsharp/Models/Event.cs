namespace MiddleCsharp.Models;

public class Event
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }

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

    public Event Clone() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
        StartAt = StartAt,
        EndAt = EndAt,
        TotalSeats = TotalSeats,
        AvailableSeats = AvailableSeats
    };
}
