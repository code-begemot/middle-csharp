using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public interface IEventService
{
    Event? GetById(Guid id);
    IReadOnlyList<Event> GetAll();
    Event Create(Event newEvent);
    bool Update(Guid id, Event updatedEvent);
    bool Delete(Guid id);
}