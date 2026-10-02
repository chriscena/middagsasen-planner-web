using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Competencies;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompetenciesController : ControllerBase
    {
        public CompetenciesController(ICompetencyService competencyService, ICurrentUserService currentUser)
        {
            CompetencyService = competencyService;
            CurrentUser = currentUser;
        }

        public ICompetencyService CompetencyService { get; }
        public ICurrentUserService CurrentUser { get; }

        [HttpGet, Authorize]
        [ProducesResponseType(typeof(IEnumerable<CompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IEnumerable<CompetencyResponse>> GetAll()
        {
            return await CompetencyService.GetCompetencies();
        }

        [HttpGet("{id}"), Authorize]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<CompetencyResponse> Get(int id)
        {
            return await CompetencyService.GetCompetencyById(id);
        }

        [HttpPost]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CompetencyRequest request)
        {
            var competency = await CompetencyService.CreateCompetency(request);
            return Created($"/api/competencies/{competency.Id}", competency);
        }

        [HttpPut("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<CompetencyResponse> Update(int id, [FromBody] CompetencyRequest request)
        {
            return await CompetencyService.UpdateCompetency(id, request);
        }

        [HttpDelete("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<CompetencyResponse> Delete(int id)
        {
            return await CompetencyService.DeleteCompetency(id);
        }

        [HttpGet("user/{userId}"), Authorize]
        [ProducesResponseType(typeof(IEnumerable<UserCompetencyResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IEnumerable<UserCompetencyResponse>> GetUserCompetencies(int userId)
        {
            if (!CurrentUser.IsAdmin && userId != CurrentUser.UserId)
                throw new ForbiddenAccessException();

            return await CompetencyService.GetUserCompetencies(userId);
        }

        [HttpPost("user"), Authorize]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddUserCompetency([FromBody] UserCompetencyRequest request)
        {
            if (!CurrentUser.IsAdmin && request.UserId != CurrentUser.UserId)
                throw new ForbiddenAccessException();

            var userCompetency = await CompetencyService.AddUserCompetency(request);
            return Created($"/api/competencies/user/{userCompetency.Id}", userCompetency);
        }

        [HttpPut("user/{userCompetencyId}/approve"), Authorize]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserCompetencyResponse> ApproveUserCompetency(int userCompetencyId, [FromBody] ApproveCompetencyRequest request)
        {
            var userCompetency = await CompetencyService.GetUserCompetencyById(userCompetencyId);

            if (!CurrentUser.IsAdmin && !await CompetencyService.IsApprover(userCompetency.CompetencyId, CurrentUser.UserId))
                throw new ForbiddenAccessException();

            return await CompetencyService.ApproveUserCompetency(userCompetencyId, request);
        }

        [HttpDelete("user/{userCompetencyId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<UserCompetencyResponse> RevokeUserCompetency(int userCompetencyId)
        {
            return await CompetencyService.RevokeUserCompetency(userCompetencyId);
        }

        [HttpPost("{id}/approvers/{userId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyApproverResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddApprover(int id, int userId)
        {
            var approver = await CompetencyService.AddApprover(id, userId);
            return Created($"/api/competencies/{id}/approvers/{approver.Id}", approver);
        }

        [HttpDelete("approvers/{approverId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task RemoveApprover(int approverId)
        {
            await CompetencyService.RemoveApprover(approverId);
        }
    }
}
