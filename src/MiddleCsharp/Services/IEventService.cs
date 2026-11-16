using MiddleCsharp.DTOs;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public interface IEventService
{
    Event GetById(Guid id);
    bool Exists(Guid id);
    PaginatedResult<Event> GetAll(EventFilter filter, int page, int pageSize);
    Event Create(Event newEvent);
    void Update(Guid id, Event updatedEvent);
    void Delete(Guid id);
}
