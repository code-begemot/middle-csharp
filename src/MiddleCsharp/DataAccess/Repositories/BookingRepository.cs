using Microsoft.EntityFrameworkCore;
using MiddleCsharp.Models;

namespace MiddleCsharp.DataAccess.Repositories;

internal sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

    public async Task<Booking?> GetForProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .Include(booking => booking.Event)
            .SingleOrDefaultAsync(stored => stored.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.CreatedAt)
            .ThenBy(booking => booking.Id)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(booking, cancellationToken);
    }
}
