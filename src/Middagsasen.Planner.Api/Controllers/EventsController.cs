using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Controllers
{
    [ApiController, Authorize]
    public class EventsController : ControllerBase
    {
        public EventsController(IEventsService eventsService)
        {
            EventsService = eventsService;
        }

        public IEventsService EventsService { get; }

        [HttpGet("api/events")]
        public async Task<IActionResult> Get([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var events = await EventsService.GetEvents(start, end);
            return Ok(events);
        }

        [HttpGet("api/events/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var existingEvent
                = await EventsService.GetEventById(id);
            return existingEvent != null ? Ok(existingEvent) : NotFound();
        }

        [HttpPost("api/events")]
        public async Task<IActionResult> Create([FromBody] EventRequest request)
        {
            var response = await EventsService.CreateEvent(request);
            return Created($"/api/events/{response.Id}", response);
        }

        [HttpPut("api/events/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
        {
            var resourceType = await EventsService.UpdateEvent(id, request);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }

        [HttpDelete("api/events/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resourceType = await EventsService.DeleteEvent(id);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }

        [HttpPost("api/resources/{id}/shifts")]
        public async Task<IActionResult> Create(int id, [FromBody] ShiftRequest request)
        {
            var response = await EventsService.AddShift(id, request);
            if (response == null) return NotFound();
            return Created($"/api/shifts/{response.Id}", response);
        }

        [HttpPut("api/shifts/{id}")]
        public async Task<IActionResult> UpdateShift(int id, [FromBody] ShiftRequest request)
        {
            var shift = await EventsService.UpdateShift(id, request);
            return (shift == null) ? NotFound() : Ok(shift);
        }

        [HttpDelete("api/shifts/{id}")]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var shift = await EventsService.DeleteShift(id);
            return (shift == null) ? NotFound() : Ok(shift);
        }
    }
}
