namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    /// <summary>Vakttyper. Vedleggene på en vakttype håndteres av <see cref="Attachments.IAttachmentService"/>.</summary>
    public interface IResourceTypesService
    {
        Task<ResourceTypeResponse> CreateResourceType(ResourceTypeRequest request);
        Task<ResourceTypeResponse> DeleteResourceType(int id);
        Task<ResourceTypeResponse> GetResourceTypeById(int id);
        Task<IEnumerable<ResourceTypeResponse>> GetResourceTypes();
        Task<ResourceTypeResponse> UpdateResourceType(int id, ResourceTypeRequest request);
    }
}
