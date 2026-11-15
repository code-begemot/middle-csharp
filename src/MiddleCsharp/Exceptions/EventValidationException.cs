namespace MiddleCsharp.Exceptions;

public sealed class EventValidationException : Exception
{
    public EventValidationException(string message)
        : base(message)
    {
    }
}
