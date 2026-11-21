using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.DTOs;
using MiddleCsharp.Services;

namespace MiddleCsharp.Controllers;

[ApiController]
[Route("bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(BookingInfo),
        StatusCodes.Status200OK,
        "application/json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<ActionResult<BookingInfo>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);
        return Ok(booking);
    }
}
