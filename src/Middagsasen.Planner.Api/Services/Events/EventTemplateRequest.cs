namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplateRequest
    {
        public string Name { get; set; } = null!;
        public string EventName { get; set; } = null!;
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public IEnumerable<ResourceTemplateRequest> ResourceTemplates { get; set; } = null!;
    }
}