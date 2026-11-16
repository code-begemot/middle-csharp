namespace MiddleCsharp.Exceptions;

public sealed class EventNotFoundException : Exception
{
    public EventNotFoundException(Guid id)
        : base($"Event with id '{id}' was not found.")
    {
        Id = id;
    }

    public Guid Id { get; }
}
