using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypesService : IResourceTypesService
    {
        public ResourceTypesService(PlannerDbContext dbContext, ITrainerNotifier trainerNotifier, IStorageService storage, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            TrainerNotifier = trainerNotifier;
            Storage = storage;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public ITrainerNotifier TrainerNotifier { get; }
        public IStorageService Storage { get; }
        public ICurrentUserService CurrentUser { get; }

        private IQueryable<ResourceType> ResourceTypes => DbContext.ResourceTypes
                .Include(r => r.Trainers)
                    .ThenInclude(t => t.User)
                .Include(r => r.Files);

        public async Task<IEnumerable<ResourceTypeResponse>> GetResourceTypes()
        {
            var resourceTypes = await ResourceTypes
                .AsNoTracking()
                .Where(r => !r.Inactive)
                .ToListAsync();
            return resourceTypes.Select(Map).ToList();
        }

        public async Task<ResourceTypeResponse> GetResourceTypeById(int id)
        {
            var resourceType = await ResourceTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.ResourceTypeId == id)
                ?? throw new EntityNotFoundException();
            return Map(resourceType);
        }

        public async Task<ResourceTypeResponse> CreateResourceType(ResourceTypeRequest request)
        {
            var resourceType = new ResourceType
            {
                Name = request.Name,
                DefaultStaff = request.DefaultStaff,
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
            resourceType.DefaultStaff = request.DefaultStaff;
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
            return Map(resourceType);
        }

        /// <summary>
        /// Oppretter opplæring for en bruker på ressurstypen. Returnerer <c>null</c> uten å lagre noe hvis
        /// <see cref="TrainingRequest.TrainingCompleted"/> mangler. <c>false</c> (ønsker opplæring) varsler trenerne
        /// på SMS etter at opplæringen er lagret; en SMS-feil logges, men ruller ikke tilbake opplæringen.
        /// </summary>
        public async Task<TrainingResponse?> CreateTraining(int resourceTypeId, TrainingRequest request)
        {
            if (!request.TrainingCompleted.HasValue) return null;

            await EnsureCanManageTraining(resourceTypeId, request.UserId);

            var training = DbContext.ResourceTypeTrainings.Add(new ResourceTypeTraining
            {
                UserId = request.UserId,
                ResourceTypeId = resourceTypeId,
                TrainingComplete = request.TrainingCompleted,
                Confirmed = !request.TrainingCompleted.Value ? null : DateTime.UtcNow,
                ConfirmedBy = !request.TrainingCompleted.Value ? null : CurrentUser.UserId,
            }).Entity;

            await DbContext.SaveChangesAsync();

            if (!request.TrainingCompleted.Value)
                await TrainerNotifier.NotifyTrainingRequested(request.UserId, resourceTypeId, request.StartTime);

            return Map(training);
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

            var responseFile = DbContext.ResourceTypeFiles
                .Include(f => f.ResourceType)
                .Include(f => f.CreatedByUser)
                .Include(f => f.UpdatedByUser)
                .AsNoTracking()
                .Single(f => f.ResourceTypeFileId == newFile.ResourceTypeFileId);

            return Map(responseFile);
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

        /// <summary>
        /// Kaster <see cref="ForbiddenAccessException"/> hvis innlogget bruker ikke kan sette opplæring
        /// for brukeren på ressurstypen (se <see cref="TrainingPolicy.CanManage"/>).
        /// </summary>
        private async Task EnsureCanManageTraining(int resourceTypeId, int userId)
        {
            var actor = CurrentUser.ToActor();

            var isTrainer = await DbContext.ResourceTypeTrainers
                .AnyAsync(t => t.ResourceTypeId == resourceTypeId && t.UserId == actor.UserId);

            if (!TrainingPolicy.CanManage(actor, userId, isTrainer))
                throw new ForbiddenAccessException();
        }

        private TrainingResponse Map(ResourceTypeTraining training) => new TrainingResponse
        {
            Id = training.ResourceTypeTrainingId,
            UserId = training.UserId,
            ResourceTypeId = training.ResourceTypeId,
            ResourceTypeName = training.ResourceType?.Name,
            TrainingComplete = training.TrainingComplete,
            Confirmed = training.Confirmed?.ToSimpleIsoString(),
            ConfirmedById = training.ConfirmedBy,
            ConfirmedByName = MapFullName(training.ConfirmedByUser?.FirstName, training.ConfirmedByUser?.LastName),
        };

        private ResourceTypeResponse Map(ResourceType resourceType) => new ResourceTypeResponse
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultStaff = resourceType.DefaultStaff,
            NotificationMessage = resourceType.NotificationMessage,
            HasTraining = resourceType.Trainers.Any(),
            Trainers = resourceType.Trainers.Select(Map).ToList(),
            Files = resourceType.Files.Select(Map).ToList(),
        };

        private ResourceTypeTrainerResponse Map(ResourceTypeTrainer resourceTypeTrainer) => new ResourceTypeTrainerResponse
        {
            Id = resourceTypeTrainer.ResourceTypeTrainerId,
            UserId = resourceTypeTrainer.UserId,
            FullName = MapFullName(resourceTypeTrainer.User.FirstName, resourceTypeTrainer.User.LastName),
            PhoneNo = resourceTypeTrainer.User.UserName,
        };

        private FileInfoResponse Map(ResourceTypeFile file) => new FileInfoResponse
        {
            Id = file.ResourceTypeFileId,
            ResourceTypeId = file.ResourceTypeId,
            FileName = file.FileName,
            Description = file.Description,
            MimeType = file.MimeType,
            Created = file.Created.AsUtc().ToIsoString(),
            CreatedBy = MapFullName(file.CreatedByUser?.FirstName, file.CreatedByUser?.LastName),
            Updated = file.Updated.AsUtc().ToIsoString(),
            UpdatedBy = MapFullName(file.UpdatedByUser?.FirstName, file.UpdatedByUser?.LastName),
        };

        private string MapFullName(string? firstName, string? lastName)
        {
            return $"{firstName ?? ""} {lastName ?? ""}".Trim();
        }
    }
}
