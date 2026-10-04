using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class ApprovedByResponse
    {
        public int WorkHourId { get; set; }
        public int? ApprovedBy { get; set; }
        public ApprovalStatus? ApprovalStatus { get; set; }
        public DateTime? ApprovedTime { get; set; }
    }
}
