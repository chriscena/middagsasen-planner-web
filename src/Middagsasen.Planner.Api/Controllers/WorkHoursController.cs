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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateWorkHourRequest request)
        {
            var response = await WorkHoursService.CreateWorkHour(request);
            return Created($"/api/workhours/{response.WorkHourId}", response);
        }

        [HttpPatch("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int workHourId, [FromBody] UpdateWorkHourRequest request)
        {
            return Ok(await WorkHoursService.UpdateWorkHour(workHourId, request));
        }

        [HttpPatch("{workHourId}/ApprovedBy")]
        [ProducesResponseType<ApprovedByResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateApprovedBy(int workHourId, [FromBody] ApprovedByRequest request)
        {
            return Ok(await WorkHoursService.UpdateApprovedBy(workHourId, request));
        }

        [HttpDelete("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(int workHourId)
        {
            return Ok(await WorkHoursService.DeleteWorkHour(workHourId));
        }

        [HttpGet]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType<PagedResponse<WorkHourResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get(int? page, int? pageSize, int? approved)
        {
            return Ok(await WorkHoursService.GetWorkHours(approved, page, pageSize));
        }

        [HttpGet("{workHourId}")]
        [ProducesResponseType<WorkHourResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int workHourId)
        {
            return Ok(await WorkHoursService.GetWorkHourById(workHourId));
        }

        [HttpGet("User/{userId}")]
        [ProducesResponseType<PagedResponse<WorkHourResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByUserId(int userId, int? page, int? pageSize, int? approved)
        {
            return Ok(await WorkHoursService.GetWorkHoursByUser(userId, approved, page, pageSize));
        }

        [HttpGet("Sum")]
        [ProducesResponseType<WorkHourSumResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSum(int? userId = null)
        {
            return Ok(await WorkHoursService.GetWorkHoursSum(userId));
        }

        [HttpGet("Sum/All")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType<IEnumerable<UserWorkHourSumResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSumPerUser()
        {
            return Ok(await WorkHoursService.GetWorkHoursSumPerUser());
        }
    }
}
