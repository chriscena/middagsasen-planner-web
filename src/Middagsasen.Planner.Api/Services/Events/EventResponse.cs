namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventResponse
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = null!;
        public string? Description { get; internal set; }
        public string StartTime { get; internal set; } = null!;
        public string EndTime { get; internal set; } = null!;
        public IEnumerable<ResourceResponse> Resources { get; set; } = null!;
    }
}