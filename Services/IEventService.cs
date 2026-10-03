// Services/IEventService.cs
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public interface IEventService
{
    Event? GetById(Guid id);
    IEnumerable<Event> GetAll();
    Event Create(Event newEvent);
    bool Update(Guid id, Event updatedEvent);
    bool Delete(Guid id);
}