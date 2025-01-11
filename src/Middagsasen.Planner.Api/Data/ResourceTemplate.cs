namespace Middagsasen.Planner.Api.Data
{
    public class ResourceTemplate
    {
        public int ResourceTemplateId { get; set; }
        public int EventTemplateId { get; set; }
        public int ResourceTypeId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int MinimumStaff { get; set; }
        public EventTemplate EventTemplate { get; set; } = null!;
        public ResourceType ResourceType { get; set; } = null!;
    }
}