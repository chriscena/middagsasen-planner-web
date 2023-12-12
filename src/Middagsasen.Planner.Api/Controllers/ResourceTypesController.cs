using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController, Authorize]
    public class ResourceTypesController : ControllerBase
    {
        public ResourceTypesController(IResourceTypesService resourceTypesService)
        {
            ResourceTypesService = resourceTypesService;
        }

        public IResourceTypesService ResourceTypesService { get; }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await ResourceTypesService.GetResourceTypes());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var resourceType = await ResourceTypesService.GetResourceTypeById(id);
            return resourceType != null ? Ok(resourceType) : NotFound();
        }

        [HttpPost]
        [Authorize(Role = Roles.Administrator)]
        public async Task<IActionResult> Create(ResourceTypeRequest request)
        {
            var resourceType = await ResourceTypesService.CreateResourceType(request);
            return Created($"/api/resourcetypes/{resourceType.Id}", resourceType);
        }

        [HttpPut("{id}")]
        [Authorize(Role = Roles.Administrator)]
        public async Task<IActionResult> Update(int id, [FromBody]ResourceTypeRequest request)
        {
            var resourceType = await ResourceTypesService.UpdateResourceType(id, request);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }

        [HttpDelete("{id}")]
        [Authorize(Role = Roles.Administrator)]
        public async Task<IActionResult> Delete(int id)
        {
            var resourceType = await ResourceTypesService.DeleteResourceType(id);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }
    }
}
