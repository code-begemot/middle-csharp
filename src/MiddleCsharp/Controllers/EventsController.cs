using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.DTOs;
using MiddleCsharp.Services;

namespace MiddleCsharp.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<PaginatedResult<EventResponse>> GetAll([FromQuery] GetEventsQuery query)
    {
        var result = _eventService.GetAll(query.ToFilter(), query.Page, query.PageSize);
        return Ok(result.ToResponse());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<EventResponse> GetById(Guid id)
    {
        return Ok(_eventService.GetById(id).ToResponse());
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<EventResponse> Create([FromBody] CreateEventRequest request)
    {
        var created = _eventService.Create(request.ToEntity());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        _eventService.Update(id, request.ToEntity());
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult Delete(Guid id)
    {
        _eventService.Delete(id);
        return NoContent();
    }
}
