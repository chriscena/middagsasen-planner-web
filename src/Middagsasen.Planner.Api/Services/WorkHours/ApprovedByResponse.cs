namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class ApprovedByResponse
    {
        public int WorkHourId { get; set; }
        public int? ApprovedBy { get; set; }
        public int? ApprovalStatus { get; set; }
        public DateTime? ApprovedTime { get; set; }
    }
}