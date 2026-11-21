using Microsoft.EntityFrameworkCore;
using MiddleCsharp.DataAccess;
using MiddleCsharp.DTOs;
using MiddleCsharp.Exceptions;
using MiddleCsharp.Models;

namespace MiddleCsharp.Services;

internal sealed class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var evt = await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

        return evt ?? throw new EventNotFoundException(id);
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
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

        IQueryable<Event> query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            var title = filter.Title.Trim().ToLower();
            query = query.Where(evt => evt.Title.ToLower().Contains(title));
        }

        if (filter.From.HasValue)
        {
            var from = Event.NormalizeToUnspecified(filter.From.Value);
            query = query.Where(evt => evt.StartAt >= from);
        }

        if (filter.To.HasValue)
        {
            var to = Event.NormalizeToUnspecified(filter.To.Value);
            query = query.Where(evt => evt.EndAt <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)page - 1) * pageSize;

        var items = skip >= totalCount
            ? new List<Event>()
            : await query
                .OrderBy(evt => evt.StartAt)
                .ThenBy(evt => evt.Id)
                .Skip((int)skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = items
        };
    }

    public async Task<Event> CreateAsync(Event newEvent, CancellationToken cancellationToken = default)
    {
        Validate(newEvent);

        if (newEvent.TotalSeats <= 0)
        {
            throw new EventValidationException("TotalSeats must be greater than 0.");
        }

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync(cancellationToken);

        return newEvent;
    }

    public async Task UpdateAsync(
        Guid id,
        Event updatedEvent,
        CancellationToken cancellationToken = default)
    {
        Validate(updatedEvent);

        var stored = await _context.Events.SingleOrDefaultAsync(evt => evt.Id == id, cancellationToken)
            ?? throw new EventNotFoundException(id);

        stored.UpdateDetails(
            updatedEvent.Title,
            updatedEvent.Description,
            updatedEvent.StartAt,
            updatedEvent.EndAt);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await SeatReservationLock.Instance.WaitAsync(cancellationToken);

        try
        {
            var stored = await _context.Events
                .Include(evt => evt.Bookings)
                .SingleOrDefaultAsync(evt => evt.Id == id, cancellationToken)
                ?? throw new EventNotFoundException(id);

            _context.Events.Remove(stored);
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SeatReservationLock.Instance.Release();
        }
    }

    private static void Validate(Event evt)
    {
        if (string.IsNullOrWhiteSpace(evt.Title))
        {
            throw new EventValidationException("Title is required.");
        }

        if (evt.Title.Length > EventConstraints.TitleMaxLength)
        {
            throw new EventValidationException(
                $"Title must be at most {EventConstraints.TitleMaxLength} characters long.");
        }

        if (evt.Description?.Length > EventConstraints.DescriptionMaxLength)
        {
            throw new EventValidationException(
                $"Description must be at most {EventConstraints.DescriptionMaxLength} characters long.");
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
