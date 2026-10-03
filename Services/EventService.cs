// Services/EventService.cs
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public class EventService : IEventService
{
    private readonly Dictionary<Guid, Event> _events = new();

    public Event? GetById(Guid id)
    {
        _events.TryGetValue(id, out var evt);
        return evt;
    }

    public IEnumerable<Event> GetAll()
    {
        return _events.Values;
    }

    public Event Create(Event newEvent)
    {
        if (newEvent.Id == Guid.Empty)
        {
            newEvent.Id = Guid.NewGuid();
        }

        _events[newEvent.Id] = newEvent;
        return newEvent;
    }

    public bool Update(Guid id, Event updatedEvent)
    {
        if (!_events.ContainsKey(id))
        {
            return false;
        }

        updatedEvent.Id = id;
        _events[id] = updatedEvent;
        return true;
    }

    public bool Delete(Guid id)
    {
        return _events.Remove(id);
    }
}