using Microsoft.AspNetCore.Mvc;
using MiddleCsharp.Models;
using MiddleCsharp.Services;

namespace middle_csharp.Controllers;

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
    public ActionResult<IEnumerable<Event>> GetAll()
    {
        return Ok(_eventService.GetAll());
    }

    // GET /events/{id}
    [HttpGet("{id:guid}")]
    public ActionResult<Event> GetById(Guid id)
    {
        var evt = _eventService.GetById(id);
        if (evt is null)
        {
            return NotFound();
        }

        return Ok(evt);
    }

    // POST /events
    [HttpPost]
    public ActionResult<Event> Create([FromBody] Event newEvent)
    {
        var created = _eventService.Create(newEvent);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /events/{id}
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] Event updatedEvent)
    {
        var success = _eventService.Update(id, updatedEvent);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE /events/{id}
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var success = _eventService.Delete(id);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}