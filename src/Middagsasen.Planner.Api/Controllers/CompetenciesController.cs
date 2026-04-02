using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Competencies;
using Middagsasen.Planner.Api.Services.Users;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompetenciesController : ControllerBase
    {
        public CompetenciesController(ICompetencyService competencyService)
        {
            CompetencyService = competencyService;
        }

        public ICompetencyService CompetencyService { get; }

        [HttpGet, Authorize]
        [ProducesResponseType(typeof(IEnumerable<CompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await CompetencyService.GetCompetencies());
        }

        [HttpGet("{id}"), Authorize]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int id)
        {
            var competency = await CompetencyService.GetCompetencyById(id);
            return competency != null ? Ok(competency) : NotFound();
        }

        [HttpPost]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CompetencyRequest request)
        {
            var competency = await CompetencyService.CreateCompetency(request);
            return Created($"/api/competencies/{competency.Id}", competency);
        }

        [HttpPut("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, [FromBody] CompetencyRequest request)
        {
            var competency = await CompetencyService.UpdateCompetency(id, request);
            return competency != null ? Ok(competency) : NotFound();
        }

        [HttpDelete("{id}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var competency = await CompetencyService.DeleteCompetency(id);
            return competency != null ? Ok(competency) : NotFound();
        }

        [HttpGet("user/{userId}"), Authorize]
        [ProducesResponseType(typeof(IEnumerable<UserCompetencyResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserCompetencies(int userId)
        {
            var user = (UserResponse)HttpContext.Items["User"]!;

            if (!user.IsAdmin && userId != user.Id)
                return new StatusCodeResult(StatusCodes.Status403Forbidden);

            return Ok(await CompetencyService.GetUserCompetencies(userId));
        }

        [HttpPost("user"), Authorize]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddUserCompetency([FromBody] UserCompetencyRequest request)
        {
            var user = (UserResponse)HttpContext.Items["User"]!;

            if (!user.IsAdmin && request.UserId != user.Id)
                return new StatusCodeResult(StatusCodes.Status403Forbidden);

            var userCompetency = await CompetencyService.AddUserCompetency(request);
            return userCompetency != null
                ? Created($"/api/competencies/user/{userCompetency.Id}", userCompetency)
                : NotFound();
        }

        [HttpPut("user/{userCompetencyId}/approve"), Authorize]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ApproveUserCompetency(int userCompetencyId, [FromBody] ApproveCompetencyRequest request)
        {
            var user = (UserResponse)HttpContext.Items["User"]!;

            var userCompetency = await CompetencyService.GetUserCompetencyById(userCompetencyId);
            if (userCompetency == null) return NotFound();

            if (!user.IsAdmin && !await CompetencyService.IsApprover(userCompetency.CompetencyId, user.Id))
                return new StatusCodeResult(StatusCodes.Status403Forbidden);

            var result = await CompetencyService.ApproveUserCompetency(userCompetencyId, user.Id, request);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpDelete("user/{userCompetencyId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(UserCompetencyResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> RevokeUserCompetency(int userCompetencyId)
        {
            var result = await CompetencyService.RevokeUserCompetency(userCompetencyId);
            return result != null ? Ok(result) : NotFound();
        }

        [HttpPost("{id}/approvers/{userId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(CompetencyApproverResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddApprover(int id, int userId)
        {
            var approver = await CompetencyService.AddApprover(id, userId);
            return approver != null
                ? Created($"/api/competencies/{id}/approvers/{approver.Id}", approver)
                : NotFound();
        }

        [HttpDelete("approvers/{approverId}")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveApprover(int approverId)
        {
            var result = await CompetencyService.RemoveApprover(approverId);
            return result ? Ok() : NotFound();
        }
    }
}
