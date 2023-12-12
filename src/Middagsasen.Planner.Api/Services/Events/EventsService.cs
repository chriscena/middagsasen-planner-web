using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IResourceTypesService, IEventsService, IEventTemplatesService
    {
        public EventsService(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

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

        public async Task<IEnumerable<ResourceTypeResponse>> GetResourceTypes()
        {
            var resourceTypes = await DbContext.ResourceTypes.AsNoTracking().Where(r => !r.Inactive).ToListAsync();
            return resourceTypes.Select(Map).ToList();
        }

        public async Task<ResourceTypeResponse?> GetResourceTypeById(int id)
        {
            var resourceType = await DbContext.ResourceTypes.AsNoTracking().SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            return (resourceType == null) ? null : Map(resourceType);
        }

        public async Task<ResourceTypeResponse> CreateResourceType(ResourceTypeRequest request)
        {
            var resourceType = new ResourceType
            {
                Name = request.Name,
                DefaultStaff = request.DefaultStaff,
            };

            DbContext.ResourceTypes.Add(resourceType);
            await DbContext.SaveChangesAsync();

            return Map(resourceType);
        }

        public async Task<ResourceTypeResponse?> UpdateResourceType(int id, ResourceTypeRequest request)
        {
            var resourceType = await DbContext.ResourceTypes.SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            if (resourceType == null) { return null; }

            resourceType.Name = request.Name;
            resourceType.DefaultStaff = request.DefaultStaff;

            await DbContext.SaveChangesAsync();

            return Map(resourceType);
        }

        public async Task<ResourceTypeResponse?> DeleteResourceType(int id)
        {
            var resourceType = await DbContext.ResourceTypes.SingleOrDefaultAsync(r => r.ResourceTypeId == id);
            if (resourceType == null) { return null; }

            resourceType.Inactive = true;

            await DbContext.SaveChangesAsync();
            return Map(resourceType);
        }

        public async Task<IEnumerable<EventResponse?>> GetEvents()
        {
            var events = await DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                .AsNoTracking()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<IEnumerable<EventResponse?>> GetEvents(DateTime start, DateTime end)
        {
            var events = await DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                .AsNoTracking()
                .Where(e => e.StartTime >= start && e.StartTime < end)
                .AsSplitQuery()
                .ToListAsync();

            return events.Select(Map).ToList();
        }

        public async Task<EventResponse?> GetEventById(int id)
        {
            var existingEvent = await DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.EventId == id);

            return (existingEvent == null) ? null : Map(existingEvent);
        }

        public async Task<EventResponse?> CreateEvent(EventRequest request)
        {
            var newEvent = new Event
            {
                Name = request.Name,
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
            var existingEvent = await DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
            .SingleOrDefaultAsync(e => e.EventId == eventId);

            if (existingEvent == null) return null;

            existingEvent.Name = request.Name;
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

            var response = await DbContext.Events
                .Include(e => e.Resources)
                    .ThenInclude(r => r.Shifts)
                        .ThenInclude(s => s.User)
                .Include(e => e.Resources)
                    .ThenInclude(r => r.ResourceType)
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
            };
            DbContext.Shifts.Add(newShift);
            await DbContext.SaveChangesAsync();

            var responseShift = await DbContext.Shifts.Include(s => s.User).AsNoTracking().SingleOrDefaultAsync(s => s.EventResourceUserId == newShift.EventResourceUserId);
            return responseShift == null ? null : Map(responseShift);
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

            var responseShift = await DbContext.Shifts.Include(s => s.User).AsNoTracking().SingleOrDefaultAsync(s => s.EventResourceUserId == shift.EventResourceUserId);
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

        public async Task<IEnumerable<EventTemplateResponse>> GetEventTemplates()
        {
            var templates = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .ThenInclude(r => r.ResourceType)
                .AsNoTracking()
                .ToListAsync();
            return templates.Select(Map).ToList();
        }

        public async Task<EventTemplateResponse?> GetEventTemplateById(int id)
        {
            var template = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                .ThenInclude(r => r.ResourceType)
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
            var existingEvent = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                    .ThenInclude(r => r.ResourceType)
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

            var response = await DbContext.EventTemplates
                .Include(e => e.ResourceTemplates)
                    .ThenInclude(r => r.ResourceType)
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


        private ResourceTypeResponse Map(ResourceType resourceType) => new ResourceTypeResponse
        {
            Id = resourceType.ResourceTypeId,
            Name = resourceType.Name,
            DefaultStaff = resourceType.DefaultStaff,
        };

        private EventResponse Map(Event evnt) => new EventResponse
        {
            Id = evnt.EventId,
            Name = evnt.Name,
            StartTime = evnt.StartTime.ToSimpleIsoString(),
            EndTime = evnt.EndTime.ToSimpleIsoString(),
            Resources = evnt.Resources.Select(Map).ToList()
        };

        private ResourceResponse Map(EventResource resource) => new ResourceResponse
        {
            Id = resource.EventResourceId,
            EventId = resource.EventId,
            ResourceType = Map(resource.ResourceType),
            StartTime = resource.StartTime.ToSimpleIsoString(),
            EndTime = resource.EndTime.ToSimpleIsoString(),
            MinimumStaff = resource.MinimumStaff,
            Shifts = resource.Shifts.Select(Map).ToList()
        };

        private ShiftResponse Map(EventResourceUser shift) => new ShiftResponse
        {
            Id = shift.EventResourceUserId,
            EventResourceId = shift.EventResourceId,
            User = Map(shift.User),
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Comment = shift.Comment,
        };

        private ShiftUserResponse Map(User user) => new ShiftUserResponse
        {
            Id = user.UserId,
            PhoneNumber = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim(),
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
    }
}
