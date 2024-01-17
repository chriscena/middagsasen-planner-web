namespace Middagsasen.Planner.Api.Services.Events
{
    public class ShiftResponse
    {
        public int Id { get; set; }
        public int EventResourceId { get; set; }
        public ShiftUserResponse User { get; set; } = null!;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Comment { get; set; }
        public bool NeedsTraining { get; set; }
    }
}