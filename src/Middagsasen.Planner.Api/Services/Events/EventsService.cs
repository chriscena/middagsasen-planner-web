using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.ResourceTypes;
using Middagsasen.Planner.Api.Services.SmsSender;
using Middagsasen.Planner.Api.Services.Storage;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IResourceTypesService, IEventsService, IEventTemplatesService
    {
        public EventsService(PlannerDbContext dbContext, ISmsSender smsSender, IStorageService storage)
        {
            DbContext = dbContext;
            SmsSender = smsSender;
            Storage = storage;
        }

        public PlannerDbContext DbContext { get; }
        public ISmsSender SmsSender { get; }
        public IStorageService Storage { get; }

        public async Task<IEnumerable<EventStatusResponse>> GetEventStatuses(int month, int year)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            var eventStatuses = await DbContext.EventStatuses.Where(e => e.StartTime >= startDate && e.StartTime < endDate).ToListAsync();

            var response = eventStatuses.Select(e => new
            {
                Date = e.StartTime.ToString("yyyy'/'MM'/'dd"),
                IsMissingStaff = e.MissingStaff == 1,
            })
                .GroupBy(r => r.Date)
                .Select(g => new EventStatusResponse
                {
                    Date = g.Key,
                    IsMissingStaff = g.Any(e => e.IsMissingStaff),
                });
            return response;
        }

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

        private IQueryable<Event> Events => DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                            .ThenInclude(u =>u.Trainings)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Trainers)
                            .ThenInclude(t => t.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                        .ThenInclude(rt => rt.Files)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Messages)
                        .ThenInclude(t => t.CreatedByUser);

        public async Task<IEnumerable<EventResponse?>> GetEvents()
        {
            var events = await Events
                .AsNoTracking()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<IEnumerable<EventResponse?>> GetEvents(DateTime start, DateTime end)
        {
            var events = await Events
                .AsNoTracking()
                .Where(e => e.StartTime >= start && e.StartTime < end)
                .AsSplitQuery()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<EventResponse?> GetEventById(int id)
        {
            var existingEvent = await Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == id);

            return (existingEvent == null) ? null : Map(existingEvent);
        }

        public async Task<IEnumerable<ShiftSeasonResponse>> GetShiftsByUserId(int id)
        {
            var shifts = await DbContext.Shifts
                .Include(e => e.Resource)
                    .ThenInclude(e => e.ResourceType)
                .AsNoTracking()
                .Where(s => s.UserId == id)
                .ToListAsync();

            var response = shifts
                .Select(s => new UserShiftResponse
                {
                    Id = s.EventResourceUserId,
                    StartDate = s.StartTime?.ToString("yyyy'-'MM'-'dd"),
                    StartTime = s.StartTime.ToSimpleIsoString(),
                    EndTime = s.EndTime.ToSimpleIsoString(),
                    ResourceName = s.Resource?.ResourceType?.Name,
                    Season = MapSeason(s.StartTime),
                    Comment = s.Comment,
                })
                .OrderByDescending(s => s.StartTime)
                .GroupBy(s => s.Season)
                .Select(s => new ShiftSeasonResponse { Label = s.Key, Shifts = [..s] })
                .ToList();

            return response;
        }

        private string MapSeason(DateTime? startTime)
        {
            if (!startTime.HasValue) return "";

            var month = startTime.Value.Month;
            var year = startTime.Value.Year;
            return (month < 7) 
                ? $"{year - 1}/{year}"
                : $"{year}/{year + 1}";
        }

        public async Task<EventResponse?> CreateEvent(EventRequest request)
        {
            var newEvent = new Event
            {
                Name = request.Name,
                Description = request.Description,
                StartTime = DateTime.Parse(request.StartTime),
                EndTime = DateTime.Parse(request.EndTime),
                Resources = request.Resources.Select(Map).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        public async Task<EventResponse?> UpdateEvent(int eventId, EventRequest request)
        {
            var existingEvent = await Events
            .SingleOrDefaultAsync(e => e.EventId == eventId);

            if (existingEvent == null) return null;

            existingEvent.Name = request.Name;
            existingEvent.Description = request.Description;
            existingEvent.StartTime = DateTime.Parse(request.StartTime);
            existingEvent.EndTime = DateTime.Parse(request.EndTime);

            foreach (var resource in request.Resources)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.Resources.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.Resources.Add(Map(resource));
                }
                else
                {
                    var resourceToUpdate = existingEvent.Resources.FirstOrDefault(r => r.EventResourceId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    resourceToUpdate.StartTime = DateTime.Parse(resource.StartTime);
                    resourceToUpdate.EndTime = DateTime.Parse(resource.EndTime);
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            var response = await Events
            .SingleOrDefaultAsync(e => e.EventId == eventId);

            return response != null ? Map(response) : null;
        }

        public async Task<EventResponse?> DeleteEvent(int id)
        {
            var existingEvent = await DbContext.Events.SingleOrDefaultAsync(e => e.EventId == id);

            if (existingEvent == null) return null;

            DbContext.Events.Remove(existingEvent);

            await DbContext.SaveChangesAsync();
            return Map(existingEvent);
        }

        public async Task<ShiftResponse?> AddShift(int eventResourceId, ShiftRequest request)
        {
            var newShift = new EventResourceUser
            {
                EventResourceId = eventResourceId,
                UserId = request.UserId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Comment = request.Comment,
            };
            DbContext.Shifts.Add(newShift);

            await DbContext.SaveChangesAsync();

            if (request.Training != null)
            {
                if (request.Training.Id == 0)
                    await CreateTraining(request.Training.ResourceTypeId, request.Training);
                else 
                    await UpdateTraining(request.Training.ResourceTypeId, request.Training);
            }

            var responseShift = await DbContext.Shifts
                .Include(s => s.Resource)
                .Include(s => s.User)
                    .ThenInclude(u => u.Trainings)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == newShift.EventResourceUserId);
            return responseShift == null ? null : Map(responseShift);
        }

        public async Task<TrainingResponse?> CreateTraining(int resourceTypeId, TrainingRequest request)
        {
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

        public async Task<ShiftResponse?> UpdateShift(int id, ShiftRequest request)
        {
            var shift = await DbContext.Shifts.Include(s => s.User).SingleOrDefaultAsync(s => s.EventResourceUserId == id);
            if (shift == null) return null;

            if (request.StartTime.HasValue)
                shift.StartTime = request.StartTime;

            if (request.EndTime.HasValue)
                shift.EndTime = request.EndTime;

            shift.UserId = request.UserId;
            shift.Comment = request.Comment;

            await DbContext.SaveChangesAsync();



            if (request.Training != null)
            {
                if (request.Training.Id == 0)
                    await CreateTraining(request.Training.ResourceTypeId, request.Training);
                else
                    await UpdateTraining(request.Training.ResourceTypeId, request.Training);
            }

            var responseShift = await DbContext.Shifts
                .Include(s => s.Resource)
                .Include(s => s.User)
                        .ThenInclude(u => u.Trainings)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
            return responseShift == null ? null : Map(responseShift);
        }

        public async Task<ShiftResponse?> DeleteShift(int id, int userId, bool isAdmin)
        {
            var shift = await DbContext.Shifts.Include(s => s.User).SingleOrDefaultAsync(s => s.EventResourceUserId == id);
            if (shift == null) return null;
            if (shift.UserId != userId && !isAdmin) return null;

            DbContext.Shifts.Remove(shift);
            await DbContext.SaveChangesAsync();

            return Map(shift);
        }

        private IQueryable<EventTemplate> EventTemplates => DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .ThenInclude(r => r.ResourceType);

        public async Task<IEnumerable<EventTemplateResponse>> GetEventTemplates()
        {
            var templates = await EventTemplates
                .AsNoTracking()
                .ToListAsync();
            return templates.Select(Map).ToList();
        }

        public async Task<EventTemplateResponse?> GetEventTemplateById(int id)
        {
            var template = await EventTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == id);
            return template != null ? Map(template) : null;
        }

        public async Task<EventTemplateResponse?> CreateEventTemplate(EventTemplateRequest request)
        {
            var newEvent = new EventTemplate
            {
                Name = request.Name,
                EventName = request.EventName,
                StartTime = DateTime.Parse(request.StartTime),
                EndTime = DateTime.Parse(request.EndTime),
                ResourceTemplates = request.ResourceTemplates.Select(Map).ToList(),
            };

            DbContext.EventTemplates.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventTemplateById(newEvent.EventTemplateId);
        }

        public async Task<EventTemplateResponse?> UpdateEventTemplate(int id, EventTemplateRequest request)
        {
            var existingEvent = await EventTemplates
            .SingleOrDefaultAsync(e => e.EventTemplateId == id);

            if (existingEvent == null) return null;

            existingEvent.Name = request.Name;
            existingEvent.EventName = request.EventName;
            existingEvent.StartTime = DateTime.Parse(request.StartTime);
            existingEvent.EndTime = DateTime.Parse(request.EndTime);

            foreach (var resource in request.ResourceTemplates)
            {
                if (resource.IsDeleted)
                {
                    var resourceToDelete = existingEvent.ResourceTemplates.FirstOrDefault(r => r.ResourceTemplateId == resource.Id);
                    if (resourceToDelete == null) continue;
                    existingEvent.ResourceTemplates.Remove(resourceToDelete);
                }
                else if (!resource.Id.HasValue)
                {
                    existingEvent.ResourceTemplates.Add(Map(resource));
                }
                else
                {
                    var resourceToUpdate = existingEvent.ResourceTemplates.FirstOrDefault(r => r.ResourceTemplateId == resource.Id);
                    if (resourceToUpdate == null) continue;
                    resourceToUpdate.ResourceTypeId = resource.ResourceTypeId;
                    resourceToUpdate.StartTime = DateTime.Parse(resource.StartTime);
                    resourceToUpdate.EndTime = DateTime.Parse(resource.EndTime);
                    resourceToUpdate.MinimumStaff = resource.MinimumStaff;
                }
            }
            await DbContext.SaveChangesAsync();

            var response = await EventTemplates
            .SingleOrDefaultAsync(e => e.EventTemplateId == id);

            return response != null ? Map(response) : null;
        }

        public async Task<EventTemplateResponse?> DeleteEventTemplate(int id)
        {
            var existingTemplate = await DbContext.EventTemplates.SingleOrDefaultAsync(e => e.EventTemplateId == id);

            if (existingTemplate == null) return null;

            DbContext.EventTemplates.Remove(existingTemplate);

            await DbContext.SaveChangesAsync();
            return Map(existingTemplate);
        }

        public async Task<EventResponse?> CreateEventFromTemplate(int templateId, EventFromTemplateRequest request)
        {
            var startDate = DateTime.Parse(request.StartDate);

            var template = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventTemplateId == templateId);

            if (template == null) return null;

            var startTime = startDate.Date + template.StartTime.TimeOfDay;
            var endTime = startDate.Date + template.EndTime.TimeOfDay;
            if (startTime > endTime) endTime = endTime.AddDays(1);

            var newEvent = new Event
            {
                Name = template.EventName,
                StartTime = startTime,
                EndTime = endTime,
                Resources = template.ResourceTemplates.Select(r =>
                {
                    var resourceStartTime = startDate.Date + r.StartTime.TimeOfDay;
                    var resourceEndTime = startDate.Date + r.EndTime.TimeOfDay;
                    if (resourceStartTime > resourceEndTime) resourceEndTime = resourceEndTime.AddDays(1);
                    return new EventResource
                    {
                        ResourceTypeId = r.ResourceTypeId,
                        StartTime = resourceStartTime,
                        EndTime = resourceEndTime,
                        MinimumStaff = r.MinimumStaff,
                    };
                }).ToList(),
            };

            DbContext.Events.Add(newEvent);
            await DbContext.SaveChangesAsync();

            return await GetEventById(newEvent.EventId);
        }

        public async Task<EventTemplateResponse?> CreateTemplateFromEvent(int id, TemplateFromEventRequest request)
        {
            var existingEvent = await DbContext.Events
                .Include(e => e.Resources)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == id);

            if (existingEvent == null) return null;

            var template = new EventTemplate
            {
                Name = request.Name,
                EventName = existingEvent.Name,
                StartTime = existingEvent.StartTime,
                EndTime = existingEvent.EndTime,
                ResourceTemplates = existingEvent.Resources.Select(r => new ResourceTemplate
                {
                    ResourceTypeId = r.ResourceTypeId,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    MinimumStaff = r.MinimumStaff,
                }).ToList(),
            };

            DbContext.EventTemplates.Add(template);
            await DbContext.SaveChangesAsync();

            return await GetEventTemplateById(template.EventTemplateId);
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

        private EventResponse Map(Event evnt) => new EventResponse
        {
            Id = evnt.EventId,
            Name = evnt.Name,
            Description = evnt.Description,
            StartTime = evnt.StartTime.ToSimpleIsoString(),
            EndTime = evnt.EndTime.ToSimpleIsoString(),
            Resources = evnt.Resources.Select(Map).OrderBy(r => r.ResourceType.Id).ThenBy(r => r.StartTime).ToList()
        };

        private ResourceResponse Map(EventResource resource) => new ResourceResponse
        {
            Id = resource.EventResourceId,
            EventId = resource.EventId,
            ResourceType = Map(resource.ResourceType),
            StartTime = resource.StartTime.ToSimpleIsoString(),
            EndTime = resource.EndTime.ToSimpleIsoString(),
            MinimumStaff = resource.MinimumStaff,
            Shifts = resource.Shifts.Select(Map).ToList(),
            Messages = resource.Messages.Select(Map).ToList()
        };

        private ShiftResponse Map(EventResourceUser shift) => new ShiftResponse
        {
            Id = shift.EventResourceUserId,
            EventResourceId = shift.EventResourceId,
            User = Map(shift.User),
            NeedsTraining = shift.User.Trainings.Any(t => t.ResourceTypeId == shift.Resource.ResourceTypeId && t.TrainingComplete.HasValue && !t.TrainingComplete.Value),
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Comment = shift.Comment,
        };

        private MessageResponse Map(EventResourceMessage message) => new MessageResponse
        {
            Id = message.EventResourceMessageId,
            EventResourceId = message.EventResourceId,
            CreatedBy = Map(message.CreatedByUser),
            Created = message.Created.AsUtc().ToIsoString(),
            Message = message.Message,
        };

        private ShiftUserResponse Map(User user) => new ShiftUserResponse
        {
            Id = user.UserId,
            PhoneNumber = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
            Trainings = user.Trainings?.Select(Map).ToList(),
        };

        private EventResource Map(ResourceRequest request)
        {
            var resource = new EventResource
            {
                ResourceTypeId = request.ResourceTypeId,
                StartTime = DateTime.Parse(request.StartTime),
                EndTime = DateTime.Parse(request.EndTime),
                MinimumStaff = request.MinimumStaff,
            };
            if (request.Id.HasValue)
            {
                resource.EventResourceId = request.Id.Value;
            }
            return resource;
        }

        private EventTemplateResponse Map(EventTemplate template) => new EventTemplateResponse
        {
            Id = template.EventTemplateId,
            Name = template.Name,
            EventName = template.EventName,
            StartTime = template.StartTime.ToSimpleIsoString(),
            EndTime = template.EndTime.ToSimpleIsoString(),
            ResourceTemplates = template.ResourceTemplates?.Select(Map),
        };

        private ResourceTemplateResponse Map(ResourceTemplate template) => new ResourceTemplateResponse
        {
            Id = template.ResourceTemplateId,
            ResourceType = Map(template.ResourceType),
            StartTime = template.StartTime.ToSimpleIsoString(),
            EndTime = template.EndTime.ToSimpleIsoString(),
            MinimumStaff = template.MinimumStaff,
        };

        private ResourceTemplate Map(ResourceTemplateRequest resource)
        {
            var template = new ResourceTemplate
            {
                ResourceTypeId = resource.ResourceTypeId,
                StartTime = DateTime.Parse(resource.StartTime),
                EndTime = DateTime.Parse(resource.EndTime),
                MinimumStaff = resource.MinimumStaff,
            };
            if (resource.Id.HasValue)
            {
                template.EventTemplateId = resource.Id.Value;
            }
            return template;
        }

        public async Task<FileInfoResponse> AddFile(int id, FileUploadRequest request)
        {
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

        public async Task<IEnumerable<MessageResponse>> GetMessages(int eventResourceId)
        {
            var messages = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .Where(m => m.EventResourceId == eventResourceId)
                .ToListAsync();

            return messages.Select(Map).ToList();
        }

        public async Task<MessageResponse?> AddMessage(int id, MessageRequest request)
        {
            var message = new EventResourceMessage
            {
                Message = request.Message,
                EventResourceId = id,
                Created = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
            };
            DbContext.Messages.Add(message);
            await DbContext.SaveChangesAsync();

            var response = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .AsNoTracking()
                .SingleAsync(m => m.EventResourceMessageId == message.EventResourceMessageId);
            return Map(response);
        }

        public async Task<MessageResponse?> DeleteMessage(int id, int eventResourceId)
        {
            var message = await DbContext.Messages
                .Include(m => m.CreatedByUser)
                .SingleOrDefaultAsync(m => m.EventResourceId == eventResourceId && m.EventResourceMessageId == id);

            if (message == null) return null;

            DbContext.Remove(message);
            await DbContext.SaveChangesAsync();

            return Map(message);
        }

        public async Task<MinimumStaffResponse?> UpdateMinimumStaff(int id, MinimumStaffRequest request)
        {
            var eventResource = await DbContext.EventResource
                .SingleOrDefaultAsync(er => er.EventResourceId == id);

            if (eventResource == null) return null;

            eventResource.MinimumStaff = request.MinimumStaff;
            await DbContext.SaveChangesAsync();

            return new MinimumStaffResponse
            {
                EventResourceId = eventResource.EventResourceId,
                MinimumStaff = eventResource.MinimumStaff
            };
        }
    }
}
