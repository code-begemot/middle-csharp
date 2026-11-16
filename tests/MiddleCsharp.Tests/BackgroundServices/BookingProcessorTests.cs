using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiddleCsharp.BackgroundServices;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;

namespace MiddleCsharp.Tests.BackgroundServices;

public class BookingProcessorTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 5, TimeSpan.Zero);

    private readonly InMemoryBookingStore _store = new();
    private readonly FixedTimeProvider _timeProvider = new(Now);

    [Fact]
    public async Task ProcessPendingAsync_PendingBooking_ConfirmsIt()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        var stored = _store.GetById(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, stored!.Status);
        Assert.Equal(Now.UtcDateTime, stored.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_NoPendingBookings_ReturnsZero()
    {
        // Arrange
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task ProcessPendingAsync_MultiplePendingBookings_ConfirmsAll()
    {
        // Arrange
        var first = Booking.CreatePending(EventId, CreatedAt);
        var second = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(first);
        _store.Add(second);
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, processed);
        Assert.Empty(_store.GetPending());
        Assert.Equal(BookingStatus.Confirmed, _store.GetById(first.Id)!.Status);
        Assert.Equal(BookingStatus.Confirmed, _store.GetById(second.Id)!.Status);
    }

    [Fact]
    public async Task ProcessPendingAsync_AlreadyProcessedBooking_IsSkipped()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);
        _store.TryUpdateStatus(
            booking.Id,
            BookingStatus.Pending,
            BookingStatus.Rejected,
            CreatedAt.AddSeconds(1));
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, processed);
        Assert.Equal(BookingStatus.Rejected, _store.GetById(booking.Id)!.Status);
    }

    [Fact]
    public async Task ProcessPendingAsync_Cancellation_ThrowsAndLeavesBookingPending()
    {
        // Arrange
        var booking = Booking.CreatePending(EventId, CreatedAt);
        _store.Add(booking);
        var processor = CreateProcessor(TimeSpan.FromSeconds(5));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessPendingAsync(cancellationTokenSource.Token));

        Assert.Equal(BookingStatus.Pending, _store.GetById(booking.Id)!.Status);
    }

    private BookingProcessor CreateProcessor(TimeSpan processingDelay)
    {
        var options = Options.Create(new BookingProcessingOptions
        {
            ProcessingDelay = processingDelay
        });

        return new BookingProcessor(
            _store,
            options,
            _timeProvider,
            NullLogger<BookingProcessor>.Instance);
    }
}
