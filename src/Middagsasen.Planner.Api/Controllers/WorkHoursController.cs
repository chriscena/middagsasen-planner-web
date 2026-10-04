using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.WorkHours;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController, Authorize]
    public class WorkHoursController : ControllerBase
    {
        public WorkHoursController(IWorkHoursService workHoursService)
        {
            WorkHoursService = workHoursService;
        }

        public IWorkHoursService WorkHoursService { get; }

        [HttpPost]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateWorkHourRequest request)
        {
            var response = await WorkHoursService.CreateWorkHour(request);
            return Created($"/api/workhours/{response.WorkHourId}", response);
        }

        [HttpPatch("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<WorkHourResponse> Update(int workHourId, [FromBody] UpdateWorkHourRequest request)
        {
            return await WorkHoursService.UpdateWorkHour(workHourId, request);
        }

        [HttpPatch("{workHourId}/ApprovedBy")]
        [ProducesResponseType<ApprovedByResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ApprovedByResponse> UpdateApprovedBy(int workHourId, [FromBody] ApprovedByRequest request)
        {
            return await WorkHoursService.UpdateApprovedBy(workHourId, request);
        }

        /// <summary>Sletter en timeføring.</summary>
        /// <returns>Den slettede føringen. Alle tilgangsflagg (canEdit, canDelete, canApprove, canResetStatus) er false.</returns>
        [HttpDelete("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<WorkHourResponse> Delete(int workHourId)
        {
            return await WorkHoursService.DeleteWorkHour(workHourId);
        }

        [HttpGet]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType<PagedResponse<WorkHourResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<PagedResponse<WorkHourResponse>> Get(int? page, int? pageSize, ApprovalFilter? approved, int? season, int? userId)
        {
            return await WorkHoursService.GetWorkHours(userId, approved, season, page, pageSize);
        }

        [HttpGet("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<WorkHourResponse> GetById(int workHourId)
        {
            return await WorkHoursService.GetWorkHourById(workHourId);
        }

        [HttpGet("User/{userId}")]
        [ProducesResponseType<PagedResponse<WorkHourResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<PagedResponse<WorkHourResponse>> GetByUserId(int userId, int? page, int? pageSize, ApprovalFilter? approved, int? season)
        {
            return await WorkHoursService.GetWorkHoursByUser(userId, approved, season, page, pageSize);
        }

        [HttpGet("Sum")]
        [ProducesResponseType<WorkHourSumResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<WorkHourSumResponse> GetSum(int? userId = null, int? season = null)
        {
            return await WorkHoursService.GetWorkHoursSum(userId, season);
        }

        [HttpGet("Sum/All")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType<IEnumerable<UserWorkHourSumResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<UserWorkHourSumResponse>> GetSumPerUser()
        {
            return await WorkHoursService.GetWorkHoursSumPerUser();
        }
    }
}
