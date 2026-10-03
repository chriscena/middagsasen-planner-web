using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Endre minimum bemanning på en ressurs (<c>PATCH api/resources/{id}/minimumStaff</c>, kun admin).</summary>
    public class MinimumStaffRequest
    {
        /// <summary>Minimum bemanning. Kan ikke være negativ.</summary>
        [Range(0, int.MaxValue)]
        public int MinimumStaff { get; set; } = 0;
    }
}
