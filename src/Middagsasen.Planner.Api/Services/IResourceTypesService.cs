using Middagsasen.Planner.Api.Services.Events;

namespace Middagsasen.Planner.Api.Services
{
    public interface IResourceTypesService
    {
        Task<ResourceTypeResponse> CreateResourceType(ResourceTypeRequest request);
        Task<ResourceTypeResponse?> DeleteResourceType(int id);
        Task<ResourceTypeResponse?> GetResourceTypeById(int id);
        Task<IEnumerable<ResourceTypeResponse>> GetResourceTypes();
        Task<ResourceTypeResponse?> UpdateResourceType(int id, ResourceTypeRequest request);
    }
}