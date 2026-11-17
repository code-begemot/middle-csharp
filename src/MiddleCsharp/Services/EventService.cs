using System.Collections.Concurrent;
using MiddleCsharp.DTOs;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

public class EventService : IEventService
{
    private readonly ConcurrentDictionary<Guid, Event> _events = new();

    public Event GetById(Guid id)
    {
        if (!_events.TryGetValue(id, out var evt))
        {
            throw new EventNotFoundException(id);
        }

        return evt.Clone();
    }

    public bool Exists(Guid id) => _events.ContainsKey(id);

    public PaginatedResult<Event> GetAll(EventFilter filter, int page, int pageSize)
    {
        if (page < 1)
        {
            throw new EventValidationException("Page must be greater than 0.");
        }

        if (pageSize < 1 || pageSize > PaginationLimits.MaxPageSize)
        {
            throw new EventValidationException($"PageSize must be between 1 and {PaginationLimits.MaxPageSize}.");
        }

        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
        {
            throw new EventValidationException("From must be earlier than or equal to To.");
        }

        IEnumerable<Event> query = _events.Values.ToList();

        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            var title = filter.Title.Trim();
            query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.From.HasValue)
        {
            query = query.Where(e => e.StartAt >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(e => e.EndAt <= filter.To.Value);
        }

        var ordered = query
            .OrderBy(e => e.StartAt)
            .ThenBy(e => e.Id)
            .ToList();

        var totalCount = ordered.Count;
        var skip = ((long)page - 1) * pageSize;
        var items = skip >= totalCount
            ? new List<Event>()
            : ordered.Skip((int)skip).Take(pageSize).Select(e => e.Clone()).ToList();

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public Event Create(Event newEvent)
    {
        Validate(newEvent);

        if (newEvent.TotalSeats <= 0)
        {
            throw new EventValidationException("TotalSeats must be greater than 0.");
        }

        var stored = newEvent.Clone();
        stored.Id = Guid.NewGuid();
        stored.AvailableSeats = stored.TotalSeats;
        _events[stored.Id] = stored;
        return stored.Clone();
    }

    public void Update(Guid id, Event updatedEvent)
    {
        Validate(updatedEvent);

        var replacement = updatedEvent.Clone();
        replacement.Id = id;

        while (true)
        {
            if (!_events.TryGetValue(id, out var existing))
            {
                throw new EventNotFoundException(id);
            }

            replacement.TotalSeats = existing.TotalSeats;
            replacement.AvailableSeats = existing.AvailableSeats;

            if (_events.TryUpdate(id, replacement, existing))
            {
                return;
            }
        }
    }

    public bool TryReserveSeats(Guid id, int count = 1)
    {
        while (true)
        {
            if (!_events.TryGetValue(id, out var existing))
            {
                throw new EventNotFoundException(id);
            }

            var replacement = existing.Clone();

            if (!replacement.TryReserveSeats(count))
            {
                return false;
            }

            if (_events.TryUpdate(id, replacement, existing))
            {
                return true;
            }
        }
    }

    public void ReleaseSeats(Guid id, int count = 1)
    {
        while (true)
        {
            if (!_events.TryGetValue(id, out var existing))
            {
                throw new EventNotFoundException(id);
            }

            var replacement = existing.Clone();
            replacement.ReleaseSeats(count);

            if (_events.TryUpdate(id, replacement, existing))
            {
                return;
            }
        }
    }

    public void Delete(Guid id)
    {
        if (!_events.TryRemove(id, out _))
        {
            throw new EventNotFoundException(id);
        }
    }

    private static void Validate(Event evt)
    {
        if (string.IsNullOrWhiteSpace(evt.Title))
        {
            throw new EventValidationException("Title is required.");
        }

        if (evt.StartAt == default)
        {
            throw new EventValidationException("StartAt is required.");
        }

        if (evt.EndAt == default)
        {
            throw new EventValidationException("EndAt is required.");
        }

        if (evt.EndAt <= evt.StartAt)
        {
            throw new EventValidationException("EndAt must be later than StartAt.");
        }
    }
}
