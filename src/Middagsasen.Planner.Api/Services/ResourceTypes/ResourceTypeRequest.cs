namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypeRequest
    {
        public string Name { get; set; } = null!;
        public int DefaultStaff { get; set; }
        public IEnumerable<ResourceTypeTrainerRequest>? Trainers { get; set; }
    }
}