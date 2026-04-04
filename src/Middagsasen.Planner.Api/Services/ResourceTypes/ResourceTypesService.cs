using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Authentication;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class ResourceTypesService : IResourceTypesService
    {
        public ResourceTypesService(PlannerDbContext dbContext, ISmsSender smsSender, IStorageService storage, ICurrentUserService currentUser)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            Storage = storage;
            CurrentUser = currentUser;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
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

        public async Task<ResourceTypeResponse?> GetResourceTypeById(int id)
        {
            var resourceType = await ResourceTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            return (resourceType == null) ? null : Map(resourceType);
        }

        public async Task<ResourceTypeResponse?> CreateResourceType(ResourceTypeRequest request)
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

        public async Task<ResourceTypeResponse?> UpdateResourceType(int id, ResourceTypeRequest request)
        {
            var resourceType = await DbContext.ResourceTypes
                .Include(r => r.Trainers)
                .SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            if (resourceType == null) { return null; }

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

        public async Task<ResourceTypeResponse?> DeleteResourceType(int id)
        {
            var resourceType = await DbContext.ResourceTypes.SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            if (resourceType == null) { return null; }

            resourceType.Inactive = true;

            await DbContext.SaveChangesAsync();
            return Map(resourceType);
        }

        public async Task<TrainingResponse?> CreateTraining(int resourceTypeId, TrainingRequest request)
        {
            if (request.TrainingCompleted.HasValue && request.TrainingCompleted.Value)
            {
                request.ConfirmedBy = CurrentUser.UserId;
            }

            if (!request.TrainingCompleted.HasValue) return null;

            var training = DbContext.ResourceTypeTrainings.Add(new ResourceTypeTraining
            {
                UserId = request.UserId,
                ResourceTypeId = resourceTypeId,
                TrainingComplete = request.TrainingCompleted,
                Confirmed = !request.TrainingCompleted.Value ? null : DateTime.UtcNow,
                ConfirmedBy = !request.TrainingCompleted.Value ? null : request.ConfirmedBy,
            }).Entity;

            await DbContext.SaveChangesAsync();

            if (request.TrainingCompleted.Value) return Map(training);

            var user = await DbContext.Users
                .SingleAsync(u => u.UserId == request.UserId);

            var resourceType = await DbContext.ResourceTypes
                .SingleAsync(t => t.ResourceTypeId == resourceTypeId);

            var trainers = await DbContext.ResourceTypeTrainers
                .Include(t => t.User)
                .AsNoTracking()
                .Where(t => t.ResourceTypeId == resourceTypeId)
                .ToListAsync();

            var body = $"Hei {{0}}! {MapFullName(user.FirstName, user.LastName)} ønsker opplæring på {resourceType.Name} og er satt opp på vakt den {request.StartTime.ToString("dd'.'MM'.'yyyy")}.";

            var messages = new List<SmsMessage>();

            foreach (var trainer in trainers)
            {
                var message = new SmsMessage
                {
                    ReceiverPhoneNo = trainer.User.UserName.ToNumericPhoneNo(),
                    Body = string.Format(body, trainer.User.FirstName),
                };
                messages.Add(message);
            }

            if (messages.Any())
                await SmsSender.SendMessages(messages);
            return Map(training);
        }

        public async Task<TrainingResponse?> UpdateTraining(int resourceTypeId, TrainingRequest request)
        {
            if (!request.TrainingCompleted.HasValue) return null;


            var training = await DbContext.ResourceTypeTrainings.SingleAsync(t => t.ResourceTypeTrainingId == request.Id);

            training.TrainingComplete = request.TrainingCompleted;
            training.Confirmed = !request.TrainingCompleted.Value ? null : DateTime.UtcNow;
            training.ConfirmedBy = !request.TrainingCompleted.Value ? null : request.ConfirmedBy;

            await DbContext.SaveChangesAsync();

            if (request.TrainingCompleted.Value) return Map(training);

            var user = await DbContext.Users
                .SingleAsync(u => u.UserId == request.UserId);

            var resourceType = await DbContext.ResourceTypes
                .SingleAsync(t => t.ResourceTypeId == resourceTypeId);

            var trainers = await DbContext.ResourceTypeTrainers
                .Include(t => t.User)
                .AsNoTracking()
                .Where(t => t.ResourceTypeId == resourceTypeId)
                .ToListAsync();

            var body = $"Hei {{0}}! {MapFullName(user.FirstName, user.LastName)} ønsker opplæring på {resourceType.Name} og er satt opp på vakt den {request.StartTime.ToString("dd'.'MM'.'yyyy")}.";

            var messages = new List<SmsMessage>();

            foreach (var trainer in trainers)
            {
                var message = new SmsMessage
                {
                    ReceiverPhoneNo = trainer.User.UserName.ToNumericPhoneNo(),
                    Body = string.Format(body, trainer.User.FirstName),
                };
                messages.Add(message);
            }

            if (messages.Any())
                await SmsSender.SendMessages(messages);
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

        private object GetContainerPath(DateTime date)
        {
            return $"resourceTypes/{date.Year}/{date.Month:0#}";
        }

        public async Task<FileResponse> GetFile(int id, int resourceTypeId)
        {
            var responseFile = DbContext.ResourceTypeFiles
                .AsNoTracking()
                .Single(f => f.ResourceTypeFileId == id && f.ResourceTypeId == resourceTypeId);

            var containerPath = GetContainerPath(responseFile.Created);
            var fileContent = await Storage.Read($"{containerPath}/{responseFile.StorageName}");

            return new FileResponse { Data = fileContent, FileName = responseFile.FileName, MimeType = responseFile.MimeType };
        }

        public async Task DeleteFile(int id, int resourceTypeId)
        {
            var fileToDelete = DbContext.ResourceTypeFiles
                .Single(f => f.ResourceTypeFileId == id && f.ResourceTypeId == resourceTypeId);

            var containerPath = GetContainerPath(fileToDelete.Created);
            await Storage.Delete($"{containerPath}/{fileToDelete.StorageName}");

            var responseFile = DbContext.Remove(fileToDelete);
            await DbContext.SaveChangesAsync();
        }

        private TrainingResponse Map(ResourceTypeTraining training) => new TrainingResponse
        {
            Id = training.ResourceTypeTrainingId,
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
