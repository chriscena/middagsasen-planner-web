namespace Middagsasen.Planner.Api.Data
{
    public class WorkHour
    {
        public int WorkHourId { get; set; }
        public int UserId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
        public int? ApprovedBy { get; set; }
        public int? ApprovalStatus { get; set; }
        public DateTime? ApprovedTime { get; set; }
    }
}