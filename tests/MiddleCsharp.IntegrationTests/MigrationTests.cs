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
    public async Task Migrate_CreatesExpectedTablesAndColumns()
    {
        // Act
        var tables = await QueryStringsAsync(
            """
            SELECT table_name AS "Value"
            FROM information_schema.tables
            WHERE table_schema = current_schema()
              AND table_type = 'BASE TABLE'
            """);

        var eventColumns = await QueryStringsAsync(
            """
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'events'
            """);

        var bookingColumns = await QueryStringsAsync(
            """
            SELECT column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'bookings'
            """);

        // Assert
        Assert.Contains("events", tables);
        Assert.Contains("bookings", tables);
        Assert.Equal(
            new[] { "available_seats", "description", "end_at", "id", "start_at", "title", "total_seats" },
            eventColumns.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "created_at", "event_id", "id", "processed_at", "status" },
            bookingColumns.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Schema_BookingEventForeignKey_UsesCascadeDelete()
    {
        // Act
        var foreignKeys = await Context.Database
            .SqlQueryRaw<ForeignKeyMetadata>(
                """
                SELECT ccu.table_name AS "ReferencedTable",
                       ccu.column_name AS "ReferencedColumn",
                       rc.delete_rule AS "DeleteRule"
                FROM information_schema.referential_constraints rc
                JOIN information_schema.key_column_usage kcu
                  ON kcu.constraint_name = rc.constraint_name
                 AND kcu.constraint_schema = rc.constraint_schema
                JOIN information_schema.constraint_column_usage ccu
                  ON ccu.constraint_name = rc.unique_constraint_name
                 AND ccu.constraint_schema = rc.unique_constraint_schema
                WHERE kcu.table_schema = current_schema()
                  AND kcu.table_name = 'bookings'
                  AND kcu.column_name = 'event_id'
                """)
            .ToListAsync();

        // Assert
        var foreignKey = Assert.Single(foreignKeys);
        Assert.Equal("events", foreignKey.ReferencedTable);
        Assert.Equal("id", foreignKey.ReferencedColumn);
        Assert.Equal("CASCADE", foreignKey.DeleteRule);
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

    private async Task<List<string>> QueryStringsAsync(string sql) =>
        await Context.Database.SqlQueryRaw<string>(sql).ToListAsync();

    private sealed class ForeignKeyMetadata
    {
        public string ReferencedTable { get; set; } = null!;
        public string ReferencedColumn { get; set; } = null!;
        public string DeleteRule { get; set; } = null!;
    }
}
