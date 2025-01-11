using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Users;

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

        [HttpGet("api/eventstatus")]
        [ProducesResponseType(typeof(IEnumerable<EventStatusResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] int month, [FromQuery] int year)
        {
            var statuses = await EventsService.GetEventStatuses(month, year);
            return Ok(statuses);
        }

        [HttpGet("api/me/shifts")]
        [ProducesResponseType(typeof(IEnumerable<UserShiftResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyShifts()
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var shifts = await EventsService.GetShiftsByUserId(user.Id);
            return Ok(shifts);
        }

        [HttpGet("api/events")]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            var events = await EventsService.GetEvents(start, end);
            return Ok(events);
        }

        [HttpGet("api/events/{id}")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int id)
        {
            var existingEvent
                = await EventsService.GetEventById(id);
            return existingEvent != null ? Ok(existingEvent) : NotFound();
        }

        [HttpPost("api/events")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] EventRequest request)
        {
            var response = await EventsService.CreateEvent(request);
            return Created($"/api/events/{response.Id}", response);
        }

        [HttpPost("api/events/template/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFromTemplate(int id, [FromBody] EventFromTemplateRequest request)
        {
            var response = await EventsService.CreateEventFromTemplate(id, request);
            return Created($"/api/events/{response.Id}", response);
        }

        [HttpPut("api/events/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
        {
            var response = await EventsService.UpdateEvent(id, request);
            return (response == null) ? NotFound() : Ok(response);
        }

        [HttpDelete("api/events/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await EventsService.DeleteEvent(id);
            return (response == null) ? NotFound() : Ok(response);
        }

        [HttpPost("api/resources/{id}/shifts")]
        [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(int id, [FromBody] ShiftRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            if (!user.IsAdmin && request.UserId != user.Id)
                return new StatusCodeResult(StatusCodes.Status403Forbidden);

            if (request.Training != null)
            {
                request.Training.ConfirmedBy = user.Id;
            }

            var response = await EventsService.AddShift(id, request);
            if (response == null) return NotFound();
            return Created($"/api/shifts/{response.Id}", response);
        }

        [HttpPost("api/resources/{id}/messages")]
        [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddMessage(int id, [FromBody] MessageRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            request.CreatedBy = user.Id;

            var response = await EventsService.AddMessage(id, request);
            if (response == null) return NotFound();
            return Created($"/api/resources/{response.EventResourceId}/messages/{response.Id}", response);
        }

        [HttpDelete("api/resources/{eventResourceId}/messages/{id}")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteMessage(int id, int eventResourceId)
        {
            var response = await EventsService.DeleteMessage(id, eventResourceId);
            if (response == null) return NotFound();
            return Ok(response);
        }

        [HttpPut("api/shifts/{id}")]
        [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateShift(int id, [FromBody] ShiftRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            if (request.UserId == 0) request.UserId = user.Id;

            if (!user.IsAdmin && request.UserId != user.Id)
                return new StatusCodeResult(StatusCodes.Status403Forbidden);

            if (request.Training != null)
            {
                request.Training.ConfirmedBy = user.Id; 
            }

            var shift = await EventsService.UpdateShift(id, request);
            return (shift == null) ? NotFound() : Ok(shift);
        }

        [HttpDelete("api/shifts/{id}")]
        [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteShift(int id)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var shift = await EventsService.DeleteShift(id, user.Id, user.IsAdmin);
            return (shift == null) ? NotFound() : Ok(shift);
        }
    }
}
