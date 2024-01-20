using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResourceTypesController : ControllerBase
    {
        public ResourceTypesController(IResourceTypesService resourceTypesService)
        {
            ResourceTypesService = resourceTypesService;
        }

        public IResourceTypesService ResourceTypesService { get; }

        [HttpGet, Authorize]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await ResourceTypesService.GetResourceTypes());
        }

        [HttpGet("{id}"), Authorize]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int id)
        {
            var resourceType = await ResourceTypesService.GetResourceTypeById(id);
            return resourceType != null ? Ok(resourceType) : NotFound();
        }

        [HttpPost]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create(ResourceTypeRequest request)
        {
            var resourceType = await ResourceTypesService.CreateResourceType(request);
            return Created($"/api/resourcetypes/{resourceType.Id}", resourceType);
        }

        [HttpPut("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, [FromBody]ResourceTypeRequest request)
        {
            var resourceType = await ResourceTypesService.UpdateResourceType(id, request);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }

        [HttpDelete("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(EventTemplateResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var resourceType = await ResourceTypesService.DeleteResourceType(id);
            return (resourceType == null) ? NotFound() : Ok(resourceType);
        }

        [HttpPost("{id}/training"), Authorize]
        [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateTraining(int id, [FromBody] TrainingRequest request)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            if (request.TrainingCompleted.HasValue && request.TrainingCompleted.Value)
            {
                request.ConfirmedBy = user.Id;
            }
            var training = await ResourceTypesService.CreateTraining(id, request);
            return Created($"{training.ResourceTypeId}/training/{training.Id}", training);
        }

        [HttpPost("{id}/files")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> UploadFile(int id, [FromForm]IFormFile file, [FromForm] string description)
        {
            var user = (UserResponse?)HttpContext.Items["User"];
            if (user == null) return Unauthorized();

            var request = new FileUploadRequest
            {
                FileInfo = file,
                Description = description,
                UserId = user.Id
            };

            var response = await ResourceTypesService.AddFile(id, request);
            return Created($"{response.ResourceTypeId}/files/{response.Id}", response);
        }

        [HttpGet("{resourceTypeId}/files/{id}")]
        public async Task<IActionResult> GetFile(int resourceTypeId, int id)
        {
            var response = await ResourceTypesService.GetFile(id, resourceTypeId);
            
            if (response == null) return NotFound();

            return File(response.Data, response.MimeType, response.FileName);
        }

        [HttpDelete("{resourceTypeId}/files/{id}")]
        [Authorize(Role = Roles.Administrator)]
        public async Task<IActionResult> DeleteFile(int resourceTypeId, int id)
        {
            await ResourceTypesService.DeleteFile(id, resourceTypeId);

            return Ok();
        }
    }

}