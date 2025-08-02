namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHourResponse
    {
        public int WorkHourId { get; internal set; }
        public int UserId { get; set; }
        public int? ShiftId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? Hours { get; set; }
        public string? Description { get; set; }
        public int? ApprovedBy { get; set; }
        public int? ApprovalStatus { get; set; }

    }
}