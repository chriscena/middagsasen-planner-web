using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Resources;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypesService : IResourceTypesService
    {
        public ResourceTypesService(PlannerDbContext dbContext, IResourceReader reader)
        {
            DbContext = dbContext;
            Reader = reader;
        }

        public PlannerDbContext DbContext { get; }
        public IResourceReader Reader { get; }

        public async Task<IEnumerable<ResourceTypeResponse>> GetResourceTypes()
        {
            return await Reader.GetResourceTypes();
        }

        public async Task<ResourceTypeResponse> GetResourceTypeById(int id)
        {
            return await Reader.GetResourceType(id)
                ?? throw new EntityNotFoundException();
        }

        public async Task<ResourceTypeResponse> CreateResourceType(ResourceTypeRequest request)
        {
            var resourceType = new ResourceType
            {
                Name = request.Name,
                DefaultShiftCount = request.DefaultShiftCount,
                NotificationMessage = request.NotificationMessage,
                Trainers = request.Trainers?.Select(t => new ResourceTypeTrainer { UserId = t.UserId }).ToList() ?? new List<ResourceTypeTrainer>(),
            };

            DbContext.ResourceTypes.Add(resourceType);
            await DbContext.SaveChangesAsync();

            return await GetResourceTypeById(resourceType.ResourceTypeId);
        }

        public async Task<ResourceTypeResponse> UpdateResourceType(int id, ResourceTypeRequest request)
        {
            var resourceType = await DbContext.ResourceTypes
                .Include(r => r.Trainers)
                .SingleOrDefaultAsync(r => r.ResourceTypeId == id)
                ?? throw new EntityNotFoundException();

            resourceType.Name = request.Name;
            resourceType.DefaultShiftCount = request.DefaultShiftCount;
            resourceType.NotificationMessage = request.NotificationMessage;

            if (request.Trainers != null)
            {
                foreach (var trainer in request.Trainers)
                {
                    if (trainer.Id > 0 && trainer.IsDeleted)
                    {
                        var trainerToDelete = resourceType.Trainers.SingleOrDefault(t => t.ResourceTypeTrainerId == trainer.Id);
                        if (trainerToDelete == null) continue;
                        resourceType.Trainers.Remove(trainerToDelete);
                    }
                    else if (trainer.Id == 0 && !trainer.IsDeleted)
                    {
                        resourceType.Trainers.Add(new ResourceTypeTrainer
                        {
                            UserId = trainer.UserId,
                        });
                    }
                }
            }
            await DbContext.SaveChangesAsync();

            return await GetResourceTypeById(resourceType.ResourceTypeId);
        }

        public async Task<ResourceTypeResponse> DeleteResourceType(int id)
        {
            var resourceType = await DbContext.ResourceTypes.SingleOrDefaultAsync(r => r.ResourceTypeId == id)
                ?? throw new EntityNotFoundException();

            resourceType.Inactive = true;

            await DbContext.SaveChangesAsync();
            return await GetResourceTypeById(id);
        }
    }
}
