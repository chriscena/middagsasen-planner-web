namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplateResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string EventName { get; set; } = null!;
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public IEnumerable<ResourceTemplateResponse>? ResourceTemplates { get; set; } = new List<ResourceTemplateResponse>();
    }
}