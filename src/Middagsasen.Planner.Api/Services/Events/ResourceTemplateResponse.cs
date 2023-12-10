namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTemplateResponse
    {
        public int Id { get; set; }
        public ResourceTypeResponse ResourceType { get; set; } = null!;
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public int MinimumStaff { get; set; }
    }
}