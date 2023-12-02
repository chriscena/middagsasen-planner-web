using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventsService : IResourceTypesService, IEventsService
    {
        public EventsService(PlannerDbContext dbContext)
        {
            DbContext = dbContext;
        }

        public PlannerDbContext DbContext { get; }

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

            }
            //existingEvent.Resources.
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
            
            shift.Comment = request.Comment;

            await DbContext.SaveChangesAsync();
            return Map(shift);
        }

        public async Task<ShiftResponse?> DeleteShift(int id)
        {
            var shift = await DbContext.Shifts.Include(s =>s.User).SingleOrDefaultAsync(s => s.EventResourceUserId == id);
            if (shift == null) return null;
            DbContext.Shifts.Remove(shift);
            await DbContext.SaveChangesAsync();

            return Map(shift);
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
    }
}
