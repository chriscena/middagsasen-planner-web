namespace Middagsasen.Planner.Api.Services.Events
{
    public class UserShiftResponse
    {
        public int Id { get; set; }
        public string? StartDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? ResourceName { get; set; }
        public string? Comment { get; set;}
    }
}