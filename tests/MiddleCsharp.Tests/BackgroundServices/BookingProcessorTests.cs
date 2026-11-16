using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiddleCsharp.BackgroundServices;
using MiddleCsharp.DataAccess;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace MiddleCsharp.Tests.BackgroundServices;

public class BookingProcessorTests
{
    private static readonly DateTime CreatedAt = new(2025, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Now =
        new(2025, 1, 10, 10, 0, 5, TimeSpan.Zero);

    private readonly EventService _eventService = new();
    private readonly InMemoryBookingStore _store = new();
    private readonly FixedTimeProvider _timeProvider = new(Now);

    [Fact]
    public async Task ProcessPendingAsync_PendingBooking_ConfirmsIt()
    {
        // Arrange
        var eventId = CreateEvent();
        var booking = Booking.CreatePending(eventId, CreatedAt);
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
        var eventId = CreateEvent();
        var first = Booking.CreatePending(eventId, CreatedAt);
        var second = Booking.CreatePending(eventId, CreatedAt);
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
        var booking = Booking.CreatePending(CreateEvent(), CreatedAt);
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
    public async Task ProcessPendingAsync_DeletedEvent_RejectsBooking()
    {
        // Arrange
        var eventId = CreateEvent();
        var booking = Booking.CreatePending(eventId, CreatedAt);
        _store.Add(booking);
        _eventService.Delete(eventId);
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        var stored = _store.GetById(booking.Id);
        Assert.Equal(BookingStatus.Rejected, stored!.Status);
        Assert.Equal(Now.UtcDateTime, stored.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_StoreFailure_ContinuesWithRemainingBookings()
    {
        // Arrange
        var eventId = CreateEvent();
        var failing = Booking.CreatePending(eventId, CreatedAt);
        var healthy = Booking.CreatePending(eventId, CreatedAt);
        var store = new ThrowingStore { ThrowingBookingId = failing.Id };
        store.Add(failing);
        store.Add(healthy);
        var processor = CreateProcessor(TimeSpan.Zero, store);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(BookingStatus.Pending, store.GetById(failing.Id)!.Status);
        Assert.Equal(BookingStatus.Confirmed, store.GetById(healthy.Id)!.Status);
    }

    [Fact]
    public async Task ProcessPendingAsync_Cancellation_ThrowsAndLeavesBookingPending()
    {
        // Arrange
        var booking = Booking.CreatePending(CreateEvent(), CreatedAt);
        _store.Add(booking);
        var processor = CreateProcessor(TimeSpan.FromSeconds(5));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessPendingAsync(cancellationTokenSource.Token));

        Assert.Equal(BookingStatus.Pending, _store.GetById(booking.Id)!.Status);
    }

    [Fact]
    public async Task ProcessPendingAsync_CancellationMidBatch_LeavesRemainingBookingsPending()
    {
        // Arrange
        var eventId = CreateEvent();
        var first = Booking.CreatePending(eventId, CreatedAt);
        var second = Booking.CreatePending(eventId, CreatedAt);
        _store.Add(first);
        _store.Add(second);
        var processor = CreateProcessor(TimeSpan.FromMilliseconds(500));
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var processing = processor.ProcessPendingAsync(cancellationTokenSource.Token);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_store.GetById(first.Id)!.Status == BookingStatus.Pending &&
               _store.GetById(second.Id)!.Status == BookingStatus.Pending &&
               DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);

        // Assert
        var statuses = new[]
        {
            _store.GetById(first.Id)!.Status,
            _store.GetById(second.Id)!.Status
        };
        Assert.Contains(BookingStatus.Confirmed, statuses);
        Assert.Contains(BookingStatus.Pending, statuses);
    }

    private Guid CreateEvent() => _eventService.Create(TestData.NewEvent()).Id;

    private BookingProcessor CreateProcessor(TimeSpan processingDelay, IBookingStore? store = null)
    {
        var options = Options.Create(new BookingProcessingOptions
        {
            ProcessingDelay = processingDelay
        });

        return new BookingProcessor(
            store ?? _store,
            _eventService,
            options,
            _timeProvider,
            NullLogger<BookingProcessor>.Instance);
    }

    private sealed class ThrowingStore : IBookingStore
    {
        private readonly InMemoryBookingStore _inner = new();

        public required Guid ThrowingBookingId { get; init; }

        public void Add(Booking booking) => _inner.Add(booking);

        public Booking? GetById(Guid id) => _inner.GetById(id);

        public IReadOnlyList<Booking> GetPending() => _inner.GetPending();

        public bool TryUpdateStatus(
            Guid id,
            BookingStatus expectedStatus,
            BookingStatus newStatus,
            DateTime processedAt)
        {
            if (id == ThrowingBookingId)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            return _inner.TryUpdateStatus(id, expectedStatus, newStatus, processedAt);
        }
    }
}
