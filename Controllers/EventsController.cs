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
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<EventResponse>> GetAll()
    {
        return Ok(_eventService.GetAll().ToResponse());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<EventResponse> GetById(Guid id)
    {
        var evt = _eventService.GetById(id);
        return evt is null ? NotFound() : Ok(evt.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<EventResponse> Create([FromBody] CreateEventRequest request)
    {
        var created = _eventService.Create(request.ToEntity());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var success = _eventService.Update(id, request.ToEntity());
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        return _eventService.Delete(id) ? NoContent() : NotFound();
    }
}
