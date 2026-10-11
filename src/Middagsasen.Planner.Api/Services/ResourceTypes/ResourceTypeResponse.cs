using Middagsasen.Planner.Api.Services.Attachments;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypeResponse
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = null!;
        public int DefaultShiftCount { get; internal set; }
        public IEnumerable<ResourceTypeTrainerResponse> Trainers { get; set; } = null!;
        public bool HasTraining { get; internal set; }
        /// <summary>Aktive vedlegg på vakttypen. Arkiverte vedlegg hentes med <c>GET api/resourcetypes/{id}/files/archived</c>.</summary>
        public IEnumerable<FileInfoResponse> Files { get; internal set; } = null!;
        public string? NotificationMessage { get; internal set; }
    }
}
