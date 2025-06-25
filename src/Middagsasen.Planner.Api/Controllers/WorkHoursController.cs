using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.WorkHours;

namespace Middagsasen.Planner.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkHoursController : ControllerBase
    {
        public WorkHoursController(IWorkHoursService workHoursService)
        {
            WorkHoursService = workHoursService;
        }

        public IWorkHoursService WorkHoursService { get; }


        [HttpPost]
        [ProducesResponseType(typeof(WorkHourResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] WorkHourRequest request)
        {
            var response = await WorkHoursService.CreateWorkHour(request);
            if (response == null)
            {
                return BadRequest("Failed to create work timer.");
            }
            return Created($"/{response.WorkHourId}", response);
        }

        [HttpPut("{workHourId}")]
        [ProducesResponseType(typeof(WorkHourResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int workHourId, [FromBody] WorkHourRequest request)
        {
            var response = await WorkHoursService.UpdateWorkHourById(workHourId, request);
            return (response == null) ? NotFound() : Ok(response);
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<WorkHourResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var workHours = await WorkHoursService.GetWorkHours();
            return Ok(workHours);
        }

        [HttpGet("{workHourId}")]
        [ProducesResponseType(typeof(WorkHourResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(int workHourId)
        {
            var existingWorkHour = await WorkHoursService.GetWorkHourById(workHourId);
            return existingWorkHour != null ? Ok(existingWorkHour) : NotFound();
        }

        [HttpGet("User/{userId}")]
        [ProducesResponseType(typeof(PagedResponse<WorkHourResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByUserId(int userId, int? page, int? pageSize, int? size, int? from, int? approved)
        {
            var existingWorkHour = await WorkHoursService.GetWorkHoursByUser(userId, page, pageSize, size, from, approved);
            return existingWorkHour != null ? Ok(existingWorkHour) : NotFound();
        }

        [HttpGet("User/{userId}/EndTime")]
        [ProducesResponseType(typeof(WorkHourResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActive(int userId)
        {
            var existingWorkHour = await WorkHoursService.GetActiveWorkHour(userId);
            return Ok(existingWorkHour);
        }

        [HttpPatch("{workHourId}/EndTime")]
        [ProducesResponseType(typeof(EndTimeResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateEndTime(int workHourId, EndTimeRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest();

                var response = await WorkHoursService.UpdateEndTime(workHourId, request);

                return response == null
                    ? NotFound()
                    : Ok(response);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpPatch("{workHourId}/ApprovedBy")]
        [ProducesResponseType(typeof(ApprovedByResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateApprovedBy(int workHourId, ApprovedByRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest();

                var response = await WorkHoursService.UpdateApprovedBy(workHourId, request);

                return response == null
                    ? NotFound()
                    : Ok(response);
            }
            catch (Exception)
            {
                throw;
            }
        }

        [HttpDelete("{workHourId}")]
        [ProducesResponseType(typeof(WorkHourResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(int workHourId)
        {
            var response = await WorkHoursService.DeleteWorkHour(workHourId);
            return (response == null) ? NotFound() : Ok(response);
        }
    }
}
