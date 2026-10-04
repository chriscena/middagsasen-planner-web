using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class ApprovedByRequest
    {
        /// <summary>Godkjent, avslått, eller null = «Ingen status» (låser opp en behandlet føring).</summary>
        public ApprovalStatus? ApprovalStatus { get; set; }
    }
}
