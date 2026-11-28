using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.Application.DTOs;
using MiddleCsharp.Application.Services;
using MiddleCsharp.Presentation.DTOs;

namespace MiddleCsharp.Presentation.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;

    public EventsController(IEventService eventService, IBookingService bookingService)
    {
        _eventService = eventService;
        _bookingService = bookingService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PaginatedResult<EventResponse>),
        StatusCodes.Status200OK,
        "application/json")]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<ActionResult<PaginatedResult<EventResponse>>> GetAll(
        [FromQuery] GetEventsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _eventService.GetAllAsync(
            query.ToFilter(),
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(EventResponse),
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
    public async Task<ActionResult<EventResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var evt = await _eventService.GetByIdAsync(id, cancellationToken);
        return Ok(evt);
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(EventResponse),
        StatusCodes.Status201Created,
        "application/json")]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status413PayloadTooLarge,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status415UnsupportedMediaType,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<ActionResult<EventResponse>> Create(
        [FromBody] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _eventService.CreateAsync(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status413PayloadTooLarge,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status415UnsupportedMediaType,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        await _eventService.UpdateAsync(id, request.ToCommand(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(
        typeof(BookingInfo),
        StatusCodes.Status202Accepted,
        "application/json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<ActionResult<BookingInfo>> Book(
        Guid id,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingService.CreateBookingAsync(id, cancellationToken);

        return AcceptedAtAction(
            nameof(BookingsController.GetById),
            "Bookings",
            new { id = booking.Id },
            booking);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError,
        "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _eventService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
