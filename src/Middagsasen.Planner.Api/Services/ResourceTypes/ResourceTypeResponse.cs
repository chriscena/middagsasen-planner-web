namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypeResponse
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = null!;
        public int DefaultStaff { get; internal set; }
        public IEnumerable<ResourceTypeTrainerResponse> Trainers { get; set; } = null!;
        public bool HasTraining { get; internal set; }
        public IEnumerable<FileInfoResponse> Files { get; internal set; } = null!;
        public string? NotificationMessage { get; internal set; }
    }
}