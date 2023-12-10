namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTemplateRequest
    {
        public int? Id { get; set; }
        public int ResourceTypeId { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public int MinimumStaff { get; set; }
        public bool IsDeleted { get; set; }
    }
}