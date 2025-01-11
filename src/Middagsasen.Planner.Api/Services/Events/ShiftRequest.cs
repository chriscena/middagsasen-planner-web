using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class ShiftRequest
    {
        public int UserId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Comment { get; set; }
        public TrainingRequest? Training { get; set; }
    }
}