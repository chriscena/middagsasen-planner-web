using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypeRequest
    {
        public string Name { get; set; } = null!;
        [Range(1, int.MaxValue)]
        public required int DefaultShiftCount { get; set; }
        public string? NotificationMessage { get; set; }
        public IEnumerable<ResourceTypeTrainerRequest>? Trainers { get; set; }
    }
}