using System.Diagnostics;
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
    public async Task ProcessPendingAsync_MultiplePendingBookings_ProcessesThemInParallel()
    {
        // Arrange
        var eventId = CreateEvent();
        var bookings = Enumerable.Range(0, 4)
            .Select(_ => Booking.CreatePending(eventId, CreatedAt))
            .ToList();

        foreach (var booking in bookings)
        {
            _store.Add(booking);
        }

        var processor = CreateProcessor(TimeSpan.FromMilliseconds(500));

        // Act
        var stopwatch = Stopwatch.StartNew();
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.Equal(4, processed);
        Assert.All(
            bookings,
            booking => Assert.Equal(BookingStatus.Confirmed, _store.GetById(booking.Id)!.Status));
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(1600),
            $"Processing took {stopwatch.ElapsedMilliseconds} ms, expected parallel execution.");
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
    public async Task ProcessPendingAsync_ConfirmFailure_RejectsBookingAndReleasesSeat()
    {
        // Arrange
        var eventId = CreateEvent(totalSeats: 2);
        _eventService.TryReserveSeats(eventId);
        var booking = Booking.CreatePending(eventId, CreatedAt);
        var store = new FailingOnConfirmStore { FailingBookingId = booking.Id };
        store.Add(booking);
        var processor = CreateProcessor(TimeSpan.Zero, store);

        // Act
        var processed = await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(BookingStatus.Rejected, store.GetById(booking.Id)!.Status);
        Assert.Equal(2, _eventService.GetById(eventId).AvailableSeats);
    }

    [Fact]
    public async Task ProcessPendingAsync_ConfirmedBooking_KeepsReservedSeat()
    {
        // Arrange
        var eventId = CreateEvent(totalSeats: 2);
        _eventService.TryReserveSeats(eventId);
        var booking = Booking.CreatePending(eventId, CreatedAt);
        _store.Add(booking);
        var processor = CreateProcessor(TimeSpan.Zero);

        // Act
        await processor.ProcessPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, _store.GetById(booking.Id)!.Status);
        Assert.Equal(1, _eventService.GetById(eventId).AvailableSeats);
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
    public async Task ProcessPendingAsync_CancellationMidBatch_LeavesBookingsPending()
    {
        // Arrange
        var eventId = CreateEvent();
        var first = Booking.CreatePending(eventId, CreatedAt);
        var second = Booking.CreatePending(eventId, CreatedAt);
        _store.Add(first);
        _store.Add(second);
        var processor = CreateProcessor(TimeSpan.FromSeconds(5));
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var processing = processor.ProcessPendingAsync(cancellationTokenSource.Token);
        await Task.Delay(50);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);

        // Assert
        Assert.Equal(BookingStatus.Pending, _store.GetById(first.Id)!.Status);
        Assert.Equal(BookingStatus.Pending, _store.GetById(second.Id)!.Status);
    }

    private Guid CreateEvent(int totalSeats = 10) =>
        _eventService.Create(TestData.NewEvent(totalSeats: totalSeats)).Id;

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

    private sealed class FailingOnConfirmStore : IBookingStore
    {
        private readonly InMemoryBookingStore _inner = new();

        public required Guid FailingBookingId { get; init; }

        public void Add(Booking booking) => _inner.Add(booking);

        public Booking? GetById(Guid id) => _inner.GetById(id);

        public IReadOnlyList<Booking> GetPending() => _inner.GetPending();

        public bool TryUpdateStatus(
            Guid id,
            BookingStatus expectedStatus,
            BookingStatus newStatus,
            DateTime processedAt)
        {
            if (id == FailingBookingId && newStatus == BookingStatus.Confirmed)
            {
                throw new InvalidOperationException("Simulated store failure.");
            }

            return _inner.TryUpdateStatus(id, expectedStatus, newStatus, processedAt);
        }
    }
}
