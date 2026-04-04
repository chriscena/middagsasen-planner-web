using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplatesService : IEventTemplatesService
    {
        public EventTemplatesService(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

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
