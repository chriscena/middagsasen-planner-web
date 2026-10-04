using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Controllers
{
    [ApiController, Authorize(Role = Roles.Administrator)]
    public class TemplatesController : ControllerBase
    {
        public IEventTemplatesService TemplatesService { get; }

        public TemplatesController(IEventTemplatesService templatesService)
        {
            TemplatesService = templatesService;
        }

        [HttpGet("api/templates")]
        [ProducesResponseType(typeof(IEnumerable<EventTemplateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<EventTemplateResponse>> Get()
        {
            return await TemplatesService.GetEventTemplates();
        }

        [HttpGet("api/templates/{id}")]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<EventTemplateResponse> Get(int id)
        {
            return await TemplatesService.GetEventTemplateById(id);
        }

        [HttpPost("api/templates")]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] EventTemplateRequest request)
        {
            var response = await TemplatesService.CreateEventTemplate(request);
            return Created($"/api/templates/{response.Id}", response);
        }

        [HttpPut("api/templates/{id}")]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<EventTemplateResponse> Update(int id, [FromBody] EventTemplateRequest request)
        {
            return await TemplatesService.UpdateEventTemplate(id, request);
        }

        [HttpDelete("api/templates/{id}")]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<EventTemplateResponse> Delete(int id)
        {
            return await TemplatesService.DeleteEventTemplate(id);
        }

        [HttpPost("api/events/{id}/template")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateTemplateFromEvent(int id, [FromBody] TemplateFromEventRequest request)
        {
            var response = await TemplatesService.CreateTemplateFromEvent(id, request);
            return Created($"/api/templates/{response.Id}", response);
        }
    }
}
