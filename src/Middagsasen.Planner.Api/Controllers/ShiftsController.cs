using Microsoft.AspNetCore.Mvc;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.Shifts;

namespace Middagsasen.Planner.Api.Controllers
{
    /// <summary>
    /// Vaktpåmelding. Alle endepunktene returnerer <see cref="ShiftResult"/> med hele ressursen etter endringen
    /// (med flagg for innlogget bruker), og 200 OK, også ved påmelding: svaret er ressursen, ikke en ny vakt.
    /// Endring av minimum bemanning ligger også her, siden den påvirker kapasitetsreglene, og returnerer ressursen.
    /// </summary>
    [ApiController, Authorize]
    public class ShiftsController : ControllerBase
    {
        public ShiftsController(IShiftService shiftService)
        {
            ShiftService = shiftService;
        }

        public IShiftService ShiftService { get; }

        /// <summary>
        /// Ta vakt på ressursen (eller sett opp en annen bruker, kun admin). Opplæringen (<c>trainingCompleted</c>) lagres i
        /// samme transaksjon som vakta.
        /// </summary>
        [HttpPost("api/resources/{id}/shifts")]
        [ProducesResponseType(typeof(ShiftResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ShiftResult> SignUp(int id, [FromBody] SignUpRequest request)
        {
            return await ShiftService.SignUp(id, request);
        }

        /// <summary>
        /// Endre tider, kommentar og (kun admin) eier av vakta. Opplæringen til eieren etter endringen (<c>trainingCompleted</c>)
        /// lagres i samme transaksjon som vakta.
        /// </summary>
        [HttpPut("api/shifts/{id}")]
        [ProducesResponseType(typeof(ShiftResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ShiftResult> Change(int id, [FromBody] ChangeShiftRequest request)
        {
            return await ShiftService.Change(id, request);
        }

        /// <summary>Sette opplæringen til eieren av vakta (eier, trener for ressurstypen eller admin).</summary>
        [HttpPut("api/shifts/{id}/training")]
        [ProducesResponseType(typeof(ShiftResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ShiftResult> SetTraining(int id, [FromBody] SetTrainingRequest request)
        {
            return await ShiftService.SetTraining(id, request);
        }

        /// <summary>Trekke seg fra / slette vakta (eier eller admin).</summary>
        [HttpDelete("api/shifts/{id}")]
        [ProducesResponseType(typeof(ShiftResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ShiftResult> Withdraw(int id)
        {
            return await ShiftService.Withdraw(id);
        }

        /// <summary>Endre minimum bemanning på ressursen (kun admin). Returnerer ressursen med oppdaterte flagg.</summary>
        [HttpPatch("api/resources/{eventResourceId}/minimumStaff")]
        [Authorize(Role = Roles.Administrator)]
        [ProducesResponseType(typeof(ResourceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ResourceResponse> SetMinimumStaff(int eventResourceId, [FromBody] MinimumStaffRequest request)
        {
            return await ShiftService.SetMinimumStaff(eventResourceId, request);
        }
    }
}
