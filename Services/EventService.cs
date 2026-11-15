using System.Collections.Concurrent;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public class EventService : IEventService
{
    private readonly ConcurrentDictionary<Guid, Event> _events = new();

    public Event? GetById(Guid id)
    {
        _events.TryGetValue(id, out var evt);
        return evt;
    }

    public IReadOnlyList<Event> GetAll()
    {
        return _events.Values.ToList();
    }

    public Event Create(Event newEvent)
    {
        newEvent.Id = Guid.NewGuid();
        _events[newEvent.Id] = newEvent;
        return newEvent;
    }

    public bool Update(Guid id, Event updatedEvent)
    {
        if (!_events.TryGetValue(id, out var existing))
        {
            return false;
        }

        updatedEvent.Id = id;
        return _events.TryUpdate(id, updatedEvent, existing);
    }

    public bool Delete(Guid id)
    {
        return _events.TryRemove(id, out _);
    }
}