namespace Middagsasen.Planner.Api.Data
{
    public class EventTemplate
    {
        public int EventTemplateId { get; set; }
        public string Name { get; set; } = null!;
        public string EventName { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ICollection<ResourceTemplate> ResourceTemplates { get; set; } = new HashSet<ResourceTemplate>();
    }
}