namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class ApprovedByRequest
    {
        /// <summary>1 = godkjent, 2 = avslått, null = «Ingen status» (låser opp en behandlet føring).</summary>
        public int? ApprovalStatus { get; set; }
    }
}
