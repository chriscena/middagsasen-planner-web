using Microsoft.AspNetCore.Http;
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
        public async Task<IActionResult> Get()
        {
            var templates
                = await TemplatesService.GetEventTemplates();
            return templates != null ? Ok(templates) : NotFound();
        }

        [HttpGet("api/templates/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var template
                = await TemplatesService.GetEventTemplateById(id);
            return template != null ? Ok(template) : NotFound();
        }

        [HttpPost("api/templates")]
        public async Task<IActionResult> Create([FromBody] EventTemplateRequest request)
        {
            var response = await TemplatesService.CreateEventTemplate(request);
            return Created($"/api/templates/{response.Id}", response);
        }

        [HttpPut("api/templates/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] EventTemplateRequest request)
        {
            var template = await TemplatesService.UpdateEventTemplate(id, request);
            return (template == null) ? NotFound() : Ok(template);
        }

        [HttpDelete("api/templates/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var template = await TemplatesService.DeleteEventTemplate(id);
            return (template == null) ? NotFound() : Ok(template);
        }

        [HttpPost("api/events/{id}/template")]
        [Authorize(Role = Roles.Administrator)]
        public async Task<IActionResult> CreateTemplateFromEvent(int id, [FromBody] TemplateFromEventRequest request)
        {
            var response = await TemplatesService.CreateTemplateFromEvent(id, request);
            return Created($"/api/templates/{response.Id}", response);
        }
    }
}
