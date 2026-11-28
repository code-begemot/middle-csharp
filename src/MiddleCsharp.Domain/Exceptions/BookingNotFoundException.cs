namespace MiddleCsharp.Domain.Exceptions;

public sealed class BookingNotFoundException : NotFoundException
{
    public BookingNotFoundException(Guid id)
        : base($"Booking with id '{id}' was not found.")
    {
        Id = id;
    }

    public Guid Id { get; }
}
