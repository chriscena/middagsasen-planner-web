namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHourSumResponse
    {
        public double ApprovedHours { get; set; }
        public double PendingHours { get; set; }
        public double RejectedHours { get; set; }
    }
}