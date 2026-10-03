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

    // GET /events
    [HttpGet]
    public ActionResult<IEnumerable<EventResponse>> GetAll()
    {
        return Ok(_eventService.GetAll().ToResponse());
    }

    // GET /events/{id}
    [HttpGet("{id:guid}")]
    public ActionResult<EventResponse> GetById(Guid id)
    {
        var evt = _eventService.GetById(id);
        return evt is null ? NotFound() : Ok(evt.ToResponse());
    }

    // POST /events
    [HttpPost]
    public ActionResult<EventResponse> Create([FromBody] CreateEventRequest request)
    {
        var created = _eventService.Create(request.ToEntity());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    // PUT /events/{id}
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var success = _eventService.Update(id, request.ToEntity());
        return success ? NoContent() : NotFound();
    }

    // DELETE /events/{id}
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        return _eventService.Delete(id) ? NoContent() : NotFound();
    }
}