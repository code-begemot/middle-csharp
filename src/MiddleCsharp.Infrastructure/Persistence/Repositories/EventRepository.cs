using Microsoft.EntityFrameworkCore;
using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Domain.Entities;

namespace MiddleCsharp.Infrastructure.Persistence.Repositories;

internal sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

    public async Task<Event?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _context.Events
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

    public async Task<PaginatedResult<Event>> GetPageAsync(
        EventFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Event> query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            var title = filter.Title.Trim().ToLower();
            query = query.Where(evt => evt.Title.ToLower().Contains(title));
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value;
            query = query.Where(evt => evt.StartAt >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value;
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
            Items = items
        };
    }

    public async Task AddAsync(Event newEvent, CancellationToken cancellationToken = default)
    {
        await _context.Events.AddAsync(newEvent, cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var stored = await _context.Events
            .Include(evt => evt.Bookings)
            .SingleOrDefaultAsync(evt => evt.Id == id, cancellationToken);

        if (stored is null)
        {
            return false;
        }

        _context.Events.Remove(stored);
        return true;
    }
}
