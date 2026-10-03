namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Endre minimum bemanning på en ressurs (<c>PATCH api/resources/{id}/minimumStaff</c>, kun admin).</summary>
    public class MinimumStaffRequest
    {
        public int MinimumStaff { get; set; } = 0;
    }
}
