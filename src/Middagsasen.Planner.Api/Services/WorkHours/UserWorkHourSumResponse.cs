namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class UserWorkHourSumResponse
    {
        public int UserId { get; set; }
        public double ApprovedHours { get; set; }
        public double PendingHours { get; set; }
        public double RejectedHours { get; set; }
    }
}
