namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public IEnumerable<ResourceRequest> Resources { get; set; } = null!;
    }
}