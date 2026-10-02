using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResourceTypesController : ControllerBase
    {
        internal const string TrainingCompletedRequiredMessage = "Du må oppgi om opplæringen er fullført.";

        public ResourceTypesController(IResourceTypesService resourceTypesService, ICompetencyService competencyService)
        {
            ResourceTypesService = resourceTypesService;
            CompetencyService = competencyService;
        }

        public IResourceTypesService ResourceTypesService { get; }
        public ICompetencyService CompetencyService { get; }

        [HttpGet, Authorize]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<ResourceTypeResponse>> GetAll()
        {
            return await ResourceTypesService.GetResourceTypes();
        }

        [HttpGet("{id}"), Authorize]
        [ProducesResponseType(typeof(ResourceTypeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ResourceTypeResponse> Get(int id)
        {
            return await ResourceTypesService.GetResourceTypeById(id);
        }

        [HttpPost]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(ResourceTypeResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create(ResourceTypeRequest request)
        {
            var resourceType = await ResourceTypesService.CreateResourceType(request);
            return Created($"/api/resourcetypes/{resourceType.Id}", resourceType);
        }

        [HttpPut("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(ResourceTypeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ResourceTypeResponse> Update(int id, [FromBody]ResourceTypeRequest request)
        {
            return await ResourceTypesService.UpdateResourceType(id, request);
        }

        [HttpDelete("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(ResourceTypeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ResourceTypeResponse> Delete(int id)
        {
            return await ResourceTypesService.DeleteResourceType(id);
        }

        [HttpPost("{id}/training"), Authorize]
        [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateTraining(int id, [FromBody] TrainingRequest request)
        {
            // Servicen ignorerer bevisst opplæring uten TrainingCompleted (brukt fra vakt-endepunktene),
            // men her er opplæringen selve ressursen som opprettes, så da er forespørselen ugyldig.
            var training = await ResourceTypesService.CreateTraining(id, request)
                ?? throw new DomainValidationException(TrainingCompletedRequiredMessage);

            return Created($"{training.ResourceTypeId}/training/{training.Id}", training);
        }

        [HttpPost("{id}/files")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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

        [HttpGet("{resourceTypeId}/files/{id}"), Authorize]
        [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFile(int resourceTypeId, int id)
        {
            var response = await ResourceTypesService.GetFile(id, resourceTypeId);

            return File(response.Data, response.MimeType, response.FileName);
        }

        [HttpDelete("{resourceTypeId}/files/{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task DeleteFile(int resourceTypeId, int id)
        {
            await ResourceTypesService.DeleteFile(id, resourceTypeId);
        }

        [HttpGet("{id}/competencies"), Authorize]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeCompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<ResourceTypeCompetencyResponse>> GetCompetencies(int id)
        {
            return await CompetencyService.GetResourceTypeCompetencies(id);
        }

        [HttpPut("{id}/competencies")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<ResourceTypeCompetencyResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<ResourceTypeCompetencyResponse>> SetCompetencies(int id, [FromBody] IEnumerable<SetResourceTypeCompetencyRequest> requirements)
        {
            return await CompetencyService.SetResourceTypeCompetencies(id, requirements);
        }
    }

}