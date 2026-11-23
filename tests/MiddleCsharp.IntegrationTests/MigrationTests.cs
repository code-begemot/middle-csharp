using Microsoft.EntityFrameworkCore;

namespace MiddleCsharp.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class MigrationTests : RepositoryTestBase
{
    public MigrationTests(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Migrate_AppliesInitialCreateMigration()
    {
        // Act
        var appliedMigrations = await Context.Database.GetAppliedMigrationsAsync();

        // Assert
        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DatabaseCascade_DeletesBookings_WhenEventIsDeleted()
    {
        // Arrange
        var createdEvent = await AddEventAsync();
        var booking = await AddPendingBookingAsync(createdEvent.Id);

        await using (var deleteContext = CreateContext())
        {
            var storedEvent = await deleteContext.Events
                .SingleAsync(evt => evt.Id == createdEvent.Id);
            deleteContext.Events.Remove(storedEvent);
            await deleteContext.SaveChangesAsync();
        }

        // Assert
        await using var context = CreateContext();
        Assert.Null(await context.Events.SingleOrDefaultAsync(evt => evt.Id == createdEvent.Id));
        Assert.Null(await context.Bookings.SingleOrDefaultAsync(stored => stored.Id == booking.Id));
    }
}
