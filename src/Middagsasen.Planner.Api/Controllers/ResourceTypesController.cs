using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResourceTypesController : ControllerBase
    {
        public ResourceTypesController(IResourceTypesService resourceTypesService, ICompetencyService competencyService)
        {
            ResourceTypesService = resourceTypesService;
            CompetencyService = competencyService;
        }

        public IResourceTypesService ResourceTypesService { get; }
        public ICompetencyService CompetencyService { get; }

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
            var training = await ResourceTypesService.CreateTraining(id, request);
            return Created($"{training.ResourceTypeId}/training/{training.Id}", training);
        }

        [HttpPost("{id}/files")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> UploadFile(int id, IFormFile file, [FromForm] string description)
        {
            var request = new FileUploadRequest
            {
                FileInfo = file,
                Description = description,
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

        [HttpGet("{id}/competencies"), Authorize]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeCompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCompetencies(int id)
        {
            var competencies = await CompetencyService.GetResourceTypeCompetencies(id);
            return Ok(competencies);
        }

        [HttpPut("{id}/competencies")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeCompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SetCompetencies(int id, [FromBody] IEnumerable<SetResourceTypeCompetencyRequest> requirements)
        {
            var result = await CompetencyService.SetResourceTypeCompetencies(id, requirements);
            return Ok(result);
        }
    }

}