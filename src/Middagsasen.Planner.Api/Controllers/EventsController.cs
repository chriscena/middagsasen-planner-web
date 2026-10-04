using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Controllers
{
    [ApiController, Authorize]
    public class EventsController : ControllerBase
    {
        public EventsController(IEventsService eventsService, ICurrentUserService currentUser)
        {
            EventsService = eventsService;
            CurrentUser = currentUser;
        }

        public IEventsService EventsService { get; }
        public ICurrentUserService CurrentUser { get; }

        [HttpGet("api/eventstatus")]
        [ProducesResponseType(typeof(IEnumerable<EventStatusResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<EventStatusResponse>> Get([FromQuery] int month, [FromQuery] int year)
        {
            return await EventsService.GetEventStatuses(month, year);
        }

        [HttpGet("api/me/shifts")]
        [ProducesResponseType(typeof(IEnumerable<ShiftSeasonResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<ShiftSeasonResponse>> GetMyShifts()
        {
            return await EventsService.GetShiftsByUserId(CurrentUser.UserId);
        }

        [HttpGet("api/events")]
        [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<EventResponse>> Get([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            return await EventsService.GetEvents(start, end);
        }

        [HttpGet("api/events/{id}")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<EventResponse> Get(int id)
        {
            return await EventsService.GetEventById(id);
        }

        [HttpPost("api/events")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] EventRequest request)
        {
            var response = await EventsService.CreateEvent(request);
            return Created($"/api/events/{response.Id}", response);
        }

        [HttpPost("api/events/template/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateFromTemplate(int id, [FromBody] EventFromTemplateRequest request)
        {
            var response = await EventsService.CreateEventFromTemplate(id, request);
            return Created($"/api/events/{response.Id}", response);
        }

        [HttpPut("api/events/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<EventResponse> Update(int id, [FromBody] EventRequest request)
        {
            return await EventsService.UpdateEvent(id, request);
        }

        [HttpDelete("api/events/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<EventResponse> Delete(int id)
        {
            return await EventsService.DeleteEvent(id);
        }

        [HttpPost("api/resources/{id}/messages")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddMessage(int id, [FromBody] MessageRequest request)
        {
            var response = await EventsService.AddMessage(id, CurrentUser.UserId, request);
            return Created($"/api/resources/{response.EventResourceId}/messages/{response.Id}", response);
        }

        [HttpDelete("api/resources/{eventResourceId}/messages/{id}")]
        [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<MessageResponse> DeleteMessage(int id, int eventResourceId)
        {
            return await EventsService.DeleteMessage(id, eventResourceId);
        }
    }
}
