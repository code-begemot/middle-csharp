using Microsoft.EntityFrameworkCore;
using MiddleCsharp.Application.Abstractions.Persistence;
using MiddleCsharp.Domain.Entities;
using MiddleCsharp.Infrastructure.Persistence;
using MiddleCsharp.Infrastructure.Persistence.Repositories;
using Npgsql;

namespace MiddleCsharp.IntegrationTests;

public abstract class RepositoryTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    protected RepositoryTestBase(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    internal AppDbContext Context { get; private set; } = null!;
    internal IEventRepository Events { get; private set; } = null!;
    internal IBookingRepository Bookings { get; private set; } = null!;
    internal IUnitOfWork UnitOfWork { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Context = CreateContext();

        await Context.Database.EnsureDeletedAsync();
        await Context.Database.MigrateAsync();

        Events = new EventRepository(Context);
        Bookings = new BookingRepository(Context);
        UnitOfWork = new UnitOfWork(Context);
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
    }

    internal AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.Container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    protected async Task<Event> AddEventAsync(
        string title = "Meeting",
        DateTime? startAt = null,
        DateTime? endAt = null,
        int totalSeats = 10)
    {
        var start = startAt ?? new DateTime(2025, 1, 10, 10, 0, 0);
        var evt = new Event(
            title,
            "Sprint planning",
            start,
            endAt ?? start.AddHours(1),
            totalSeats);

        await Events.AddAsync(evt);
        await UnitOfWork.SaveChangesAsync();

        return evt;
    }

    protected async Task<Booking> AddPendingBookingAsync(
        Guid eventId,
        DateTime? createdAt = null)
    {
        var booking = Booking.CreatePending(
            eventId,
            createdAt ?? new DateTime(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc));

        await Bookings.AddAsync(booking);
        await UnitOfWork.SaveChangesAsync();

        return booking;
    }
}
