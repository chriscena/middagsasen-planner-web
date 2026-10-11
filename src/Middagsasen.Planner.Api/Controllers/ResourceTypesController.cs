using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Attachments;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Controllers
{
    /// <summary>
    /// Vakttyper, med vedleggene (<c>{id}/files</c>, håndtert av <see cref="IAttachmentService"/>) og vaktkravene på dem.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ResourceTypesController : ControllerBase
    {
        public ResourceTypesController(IResourceTypesService resourceTypesService, ICompetencyService competencyService, IAttachmentService attachmentService)
        {
            ResourceTypesService = resourceTypesService;
            CompetencyService = competencyService;
            AttachmentService = attachmentService;
        }

        public IResourceTypesService ResourceTypesService { get; }
        public ICompetencyService CompetencyService { get; }
        public IAttachmentService AttachmentService { get; }

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

        // --- Vedlegg ---

        /// <summary>
        /// Laster opp et vedlegg (multipart: <c>file</c>, <c>description</c>, <c>requiresAcknowledgement</c>, <c>isMandatory</c>).
        /// Flaggene er valgfrie og standard av; påkrevd krever at vedlegget krever kvittering.
        /// </summary>
        [HttpPost("{id}/files")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile(int id, [FromForm] FileUploadRequest request)
        {
            var response = await AttachmentService.Upload(id, request);
            return Created($"{response.ResourceTypeId}/files/{response.Id}", response);
        }

        /// <summary>Endrer beskrivelse og flagg på vedlegget. Selve filen byttes ved arkiver + last opp.</summary>
        [HttpPut("{resourceTypeId}/files/{id:int}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<FileInfoResponse> UpdateFile(int resourceTypeId, int id, [FromBody] FileMetadataRequest request)
        {
            return await AttachmentService.UpdateMetadata(resourceTypeId, id, request);
        }

        /// <summary>Arkiverte vedlegg på vakttypen, nyeste først.</summary>
        [HttpGet("{resourceTypeId}/files/archived")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<FileInfoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<FileInfoResponse>> GetArchivedFiles(int resourceTypeId)
        {
            return await AttachmentService.GetArchived(resourceTypeId);
        }

        /// <summary>Laster ned vedlegget. Arkiverte vedlegg gir 404 for ikke-admin.</summary>
        [HttpGet("{resourceTypeId}/files/{id:int}"), Authorize]
        [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFile(int resourceTypeId, int id)
        {
            var response = await AttachmentService.Download(resourceTypeId, id);

            return File(response.Data, response.MimeType, response.FileName);
        }

        /// <summary>Arkiverer vedlegget: det fjernes fra vakttypen, men kvitteringene og filen beholdes.</summary>
        [HttpDelete("{resourceTypeId}/files/{id:int}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task DeleteFile(int resourceTypeId, int id)
        {
            await AttachmentService.Archive(resourceTypeId, id);
        }

        // --- Kvitteringer ---

        /// <summary>
        /// Kvitteringstall («x av y har kvittert») for hver aktive instruks på vakttypen, med samme populasjon som
        /// rapporten med <c>all=false</c>. Ruten kolliderer ikke med <c>files/{id}</c>, som krever et heltall.
        /// </summary>
        [HttpGet("{resourceTypeId}/files/acknowledgements")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(IEnumerable<AcknowledgementSummaryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IEnumerable<AcknowledgementSummaryResponse>> GetAcknowledgementSummaries(int resourceTypeId)
        {
            return await AttachmentService.GetAcknowledgementSummaries(resourceTypeId);
        }

        /// <summary>
        /// Kvitteringsrapport for instruksen. <c>all=false</c> gir brukerne vakttypen gjelder for (har vakt på den denne
        /// sesongen), <c>all=true</c> alle aktive brukere.
        /// </summary>
        [HttpGet("{resourceTypeId}/files/{id:int}/acknowledgements")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(AcknowledgementReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<AcknowledgementReportResponse> GetAcknowledgements(int resourceTypeId, int id, [FromQuery] bool all = false)
        {
            return await AttachmentService.GetAcknowledgementReport(resourceTypeId, id, all);
        }

        /// <summary>
        /// Kvitterer for instruksen for innlogget bruker. Idempotent: finnes kvitteringen, returneres den uendret.
        /// Body (<c>{ comment? }</c>) er valgfri: en POST uten body kvitterer uten kommentar.
        /// 400 når vedlegget ikke krever kvittering eller er arkivert.
        /// </summary>
        [HttpPost("{resourceTypeId}/files/{id:int}/acknowledgements"), Authorize]
        [ProducesResponseType(typeof(FileInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<FileInfoResponse> Acknowledge(int resourceTypeId, int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AcknowledgeRequest? request)
        {
            return await AttachmentService.Acknowledge(resourceTypeId, id, request);
        }

        // --- Vaktkrav ---

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
