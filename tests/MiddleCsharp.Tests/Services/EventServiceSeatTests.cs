using MiddleCsharp.Application.Services;
using MiddleCsharp.Domain.Exceptions;

namespace MiddleCsharp.Tests.Services;

public class EventServiceSeatTests : IDisposable
{
    private readonly TestApplication _app = new();

    private IEventService EventService => _app.Get<IEventService>();
    private IBookingService BookingService => _app.Get<IBookingService>();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Create_SetsAvailableSeatsToTotalSeats()
    {
        // Act
        var created = await EventService.CreateAsync(TestData.NewCreateCommand(totalSeats: 5));

        // Assert
        Assert.Equal(5, created.TotalSeats);
        Assert.Equal(5, created.AvailableSeats);
        Assert.Equal(5, (await EventService.GetByIdAsync(created.Id)).AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_NonPositiveTotalSeats_ThrowsValidationException(int totalSeats)
    {
        // Act & Assert
        await Assert.ThrowsAsync<EventValidationException>(
            () => EventService.CreateAsync(TestData.NewCreateCommand(totalSeats: totalSeats)));
    }

    [Fact]
    public async Task Update_PreservesSeats()
    {
        // Arrange
        var created = await EventService.CreateAsync(TestData.NewCreateCommand(totalSeats: 5));
        await BookingService.CreateBookingAsync(created.Id);
        await BookingService.CreateBookingAsync(created.Id);

        // Act
        await EventService.UpdateAsync(
            created.Id,
            TestData.NewUpdateCommand("Updated"));

        // Assert
        var stored = await EventService.GetByIdAsync(created.Id);
        Assert.Equal(5, stored.TotalSeats);
        Assert.Equal(3, stored.AvailableSeats);
        Assert.Equal("Updated", stored.Title);
    }

    [Fact]
    public async Task Delete_EventWithBookings_RemovesBookings()
    {
        // Arrange
        var created = await EventService.CreateAsync(TestData.NewCreateCommand());
        var booking = await BookingService.CreateBookingAsync(created.Id);

        // Act
        await EventService.DeleteAsync(created.Id);

        // Assert
        await Assert.ThrowsAsync<BookingNotFoundException>(
            () => BookingService.GetBookingByIdAsync(booking.Id));
        await Assert.ThrowsAsync<EventNotFoundException>(
            () => EventService.GetByIdAsync(created.Id));
    }
}
