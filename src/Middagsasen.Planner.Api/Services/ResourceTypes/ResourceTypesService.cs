using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Resources;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypesService : IResourceTypesService
    {
        public ResourceTypesService(PlannerDbContext dbContext, IResourceReader reader, IStorageService storage, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            Reader = reader;
            Storage = storage;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public IResourceReader Reader { get; }
        public IStorageService Storage { get; }
        public ICurrentUserService CurrentUser { get; }

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

        public async Task<FileInfoResponse> AddFile(int id, FileUploadRequest request)
        {
            request.UserId = CurrentUser.UserId;
            var now = DateTime.UtcNow;
            var containerPath = GetContainerPath(now);
            var storageFileName = Guid.NewGuid().ToString();

            await Storage.Save($"{containerPath}/{storageFileName}", request.FileInfo.OpenReadStream());

            var newFile = new ResourceTypeFile
            {
                StorageName = storageFileName,
                FileName = request.FileInfo.FileName,
                Description = request.Description,
                MimeType  = request.FileInfo.ContentType,
                ResourceTypeId = id,
                Created = now,
                CreatedBy = request.UserId,
                Updated = now,
                UpdatedBy = request.UserId,

            };
           DbContext.ResourceTypeFiles.Add(newFile);
           await DbContext.SaveChangesAsync();

            return await Reader.GetFile(newFile.ResourceTypeFileId)
                ?? throw new EntityNotFoundException();
        }

        private string GetContainerPath(DateTime date)
        {
            return $"resourceTypes/{date.Year}/{date.Month:0#}";
        }

        public async Task<FileResponse> GetFile(int id, int resourceTypeId)
        {
            var responseFile = await DbContext.ResourceTypeFiles
                .AsNoTracking()
                .SingleOrDefaultAsync(f => f.ResourceTypeFileId == id && f.ResourceTypeId == resourceTypeId)
                ?? throw new EntityNotFoundException();

            var containerPath = GetContainerPath(responseFile.Created);
            var fileContent = await Storage.Read($"{containerPath}/{responseFile.StorageName}");

            return new FileResponse { Data = fileContent, FileName = responseFile.FileName, MimeType = responseFile.MimeType };
        }

        public async Task DeleteFile(int id, int resourceTypeId)
        {
            var fileToDelete = await DbContext.ResourceTypeFiles
                .SingleOrDefaultAsync(f => f.ResourceTypeFileId == id && f.ResourceTypeId == resourceTypeId)
                ?? throw new EntityNotFoundException();

            var containerPath = GetContainerPath(fileToDelete.Created);
            await Storage.Delete($"{containerPath}/{fileToDelete.StorageName}");

            var responseFile = DbContext.Remove(fileToDelete);
            await DbContext.SaveChangesAsync();
        }
    }
}
